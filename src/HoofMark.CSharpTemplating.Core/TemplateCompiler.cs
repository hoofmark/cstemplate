using System.Reflection;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using HoofMark.CSharpTemplating.Abstractions;
using System.Text;

namespace HoofMark.CSharpTemplating.Core;

/// <summary>
/// Compiles a template <c>.cs</c> file using Roslyn and returns a loaded
/// <see cref="CompiledTemplate"/> ready for execution.
/// Each compilation runs in an isolated <see cref="AssemblyLoadContext"/> so
/// multiple templates can be compiled and run in the same process without
/// assembly conflicts.
/// </summary>
public sealed class TemplateCompiler
{
    private readonly IEnumerable<string>? _additionalReferencePaths;
    private readonly IEnumerable<string>? _nativeLibraryPaths;

    public TemplateCompiler(
        IEnumerable<string>? additionalReferencePaths = null,
        IEnumerable<string>? nativeLibraryPaths = null)
    {
        _additionalReferencePaths = additionalReferencePaths;
        _nativeLibraryPaths       = nativeLibraryPaths;
    }

    /// <summary>
    /// Compiles the template source at <paramref name="templateFilePath"/>.
    /// </summary>
    /// <exception cref="TemplateCompilationException">
    /// Thrown if the source contains compile errors, with all diagnostics attached.
    /// </exception>
    public CompiledTemplate Compile(string templateFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateFilePath);

        if (!File.Exists(templateFilePath))
            throw new FileNotFoundException($"Template file not found: '{templateFilePath}'", templateFilePath);

        var source = File.ReadAllText(templateFilePath);
        return CompileSource(source, templateFilePath, System.Text.Encoding.UTF8);
    }

    /// <summary>
    /// Compiles template source from a string. Useful for testing.
    /// </summary>
    public CompiledTemplate CompileSource(string source, string? sourcePath = null, Encoding? encoding = null)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            path: sourcePath ?? "<source>",
            encoding: encoding ?? System.Text.Encoding.UTF8,
            options: new CSharpParseOptions(LanguageVersion.Latest));

        var references = BuildReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: $"TemplateEngine.Template.{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                nullableContextOptions: NullableContextOptions.Enable));

        // Emit to an in-memory stream
        using var peStream = new MemoryStream();
        using var pdbStream = new MemoryStream();

        var emitResult = compilation.Emit(peStream, pdbStream);

        if (!emitResult.Success)
        {
            var diagnostics = emitResult.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => new TemplateDiagnostic(
                    Severity: DiagnosticSeverityLevel.Error,
                    Message: d.GetMessage(),
                    FilePath: d.Location.SourceTree?.FilePath,
                    Line: d.Location.GetLineSpan().StartLinePosition.Line + 1,
                    Column: d.Location.GetLineSpan().StartLinePosition.Character + 1))
                .ToList();

            throw new TemplateCompilationException(
                $"Template compilation failed with {diagnostics.Count} error(s).", diagnostics);
        }

        // Load into an isolated context, passing reference paths so the
        // context can resolve them at execution time
        peStream.Seek(0, SeekOrigin.Begin);
        pdbStream.Seek(0, SeekOrigin.Begin);

        var loadContext = new TemplateAssemblyLoadContext(_additionalReferencePaths, _nativeLibraryPaths);
        var assembly = loadContext.LoadFromStream(peStream, pdbStream);

        // Find the ITemplate implementor
        var templateType = FindTemplateType(assembly, sourcePath);

        return new CompiledTemplate(templateType, loadContext);
    }

    private static Type FindTemplateType(Assembly assembly, string? sourcePath)
    {
        var iTemplateType = typeof(ITemplate);

        var candidates = assembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && t.IsAssignableTo(iTemplateType))
            .ToList();

        return candidates.Count switch
        {
            0 => throw new TemplateCompilationException(
                    $"No class implementing ITemplate was found in the template.{FormatSource(sourcePath)}" +
                    $" Make sure your class implements 'ITemplate'.",
                    []),
            1 => candidates[0],
            _ => throw new TemplateCompilationException(
                    $"Multiple classes implementing ITemplate were found in the template.{FormatSource(sourcePath)}" +
                    $" Only one is allowed. Found: {string.Join(", ", candidates.Select(t => t.Name))}",
                    [])
        };
    }

    private List<MetadataReference> BuildReferences()
    {
        var references = new List<MetadataReference>();

        // BCL + runtime references via Basic.Reference.Assemblies (covers System.*, etc.)
        // references.AddRange(Basic.Reference.Assemblies.Net80.References.All);
        references.AddRange(Basic.Reference.Assemblies.Net100.References.All);

        // The Abstractions assembly itself (ITemplate, ITemplateContext, etc.)
        references.Add(MetadataReference.CreateFromFile(typeof(ITemplate).Assembly.Location));

        // Any additional references from cstemplate.config.json
        if (_additionalReferencePaths != null)
        {
            foreach (var path in _additionalReferencePaths)
            {
                if (File.Exists(path))
                    references.Add(MetadataReference.CreateFromFile(path));
                else
                    throw new FileNotFoundException($"Additional reference assembly not found: '{path}'", path);
            }
        }

        return references;
    }

    private static string FormatSource(string? sourcePath) =>
        sourcePath != null ? $" File: '{sourcePath}'." : "";
}

/// <summary>
/// An isolated <see cref="AssemblyLoadContext"/> for a single compiled template.
/// Overrides <see cref="Load"/> to resolve additional reference assemblies by path
/// when the CLR needs to load them during template execution.
/// Disposing the <see cref="CompiledTemplate"/> unloads this context.
/// </summary>
internal sealed class TemplateAssemblyLoadContext : AssemblyLoadContext
{
    private readonly IReadOnlyDictionary<string, string> _assemblyPathIndex;
    private readonly IReadOnlyDictionary<string, string> _nativeLibraryIndex;

    /// <param name="additionalReferencePaths">
    /// The same paths passed to Roslyn as metadata references. Indexed by
    /// assembly name so they can be resolved quickly when the CLR asks for them.
    /// </param>
    /// <param name="nativeLibraryPaths">
    /// Paths to native libraries (e.g. from NuGet runtimes/ folders). Indexed
    /// by library name so they can be resolved for P/Invoke calls.
    /// </param>
    public TemplateAssemblyLoadContext(
        IEnumerable<string>? additionalReferencePaths = null,
        IEnumerable<string>? nativeLibraryPaths = null)
        : base(isCollectible: true)
    {
        _assemblyPathIndex  = BuildIndex(additionalReferencePaths);
        _nativeLibraryIndex = BuildIndex(nativeLibraryPaths);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Check our index of explicitly provided reference paths first
        if (assemblyName.Name != null &&
            _assemblyPathIndex.TryGetValue(assemblyName.Name, out var path))
        {
            return LoadFromAssemblyPath(path);
        }

        // Fall back to the default context (handles BCL, TemplateEngine.Abstractions, etc.)
        // Returning null tells the runtime to continue with normal resolution
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        // Strip extension if present — callers may pass "SNI" or "SNI.dll"
        var nameWithoutExt = Path.GetFileNameWithoutExtension(unmanagedDllName);

        if (_nativeLibraryIndex.TryGetValue(nameWithoutExt, out var path) ||
            _nativeLibraryIndex.TryGetValue(unmanagedDllName, out path))
        {
            return LoadUnmanagedDllFromPath(path);
        }

        return IntPtr.Zero;
    }

    private static IReadOnlyDictionary<string, string> BuildIndex(
        IEnumerable<string>? paths)
    {
        if (paths == null)
            return new Dictionary<string, string>();

        var index = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in paths)
        {
            var name = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrWhiteSpace(name))
                index[name] = path;
        }
        return index;
    }
}
