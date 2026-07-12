using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using HoofMark.CSharpTemplating.Abstractions;

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
    /// <param name="templateFilePath">Absolute path to the <c>.template.cs</c> file.</param>
    /// <param name="debugMode">
    /// When <c>true</c>, emits the compiled assembly and PDB to a temp directory on
    /// disk so that an attached debugger can locate the symbols and map execution
    /// back to the original source file, enabling breakpoints inside template code.
    /// </param>
    /// <exception cref="TemplateCompilationException">
    /// Thrown if the source contains compile errors, with all diagnostics attached.
    /// </exception>
    public CompiledTemplate Compile(string templateFilePath, Encoding? encoding = null, bool debugMode = false)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(templateFilePath);
        encoding ??= Encoding.UTF8;

        if (!File.Exists(templateFilePath))
            throw new FileNotFoundException(
                $"Template file not found: '{templateFilePath}'", templateFilePath);

        var source = File.ReadAllText(templateFilePath);
        return CompileSource(source, templateFilePath, encoding, debugMode);
    }

    /// <summary>
    /// Compiles template source from a string. Useful for testing.
    /// </summary>
    public CompiledTemplate CompileSource(
        string source, string? sourcePath = null, Encoding? encoding = null, bool debugMode = false)
    {
        encoding ??= Encoding.UTF8;

        // Normalise the source path to the actual filesystem casing so the
        // path embedded in the PDB matches exactly what VS Code uses for the
        // open document URI. On case-insensitive filesystems (Windows/macOS)
        // Path.GetFullPath preserves the caller's casing, which may differ.
        var normalisedPath = NormalisePathCasing(sourcePath);

        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            path: normalisedPath ?? "<source>",
            encoding: encoding,
            options: new CSharpParseOptions(LanguageVersion.Latest));

        var references = BuildReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: $"HoofMark.CSharpTemplating.Template.{Guid.NewGuid():N}",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(
                outputKind: OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Debug,
                nullableContextOptions: NullableContextOptions.Enable));

        Assembly assembly;
        var loadContext = new TemplateAssemblyLoadContext(
            _additionalReferencePaths, _nativeLibraryPaths);

        if (debugMode)
        {
            assembly = EmitToDisk(compilation, sourcePath, loadContext);
        }
        else
        {
            assembly = EmitToMemory(compilation, sourcePath, loadContext);
        }

        var templateType = FindTemplateType(assembly, sourcePath);
        return new CompiledTemplate(templateType, loadContext);
    }

    // -- Emission -------------------------------------------------------------

    /// <summary>
    /// Emits the compiled assembly and PDB to a temporary directory on disk.
    /// The debugger discovers the PDB via the path embedded in the PE header,
    /// allowing it to map execution back to the original <c>.template.cs</c> source.
    /// </summary>
    private static Assembly EmitToDisk(
        CSharpCompilation compilation,
        string? sourcePath,
        TemplateAssemblyLoadContext loadContext)
    {
        var tempDir = Path.Combine(
            Path.GetTempPath(),
            "cstemplate-debug",
            Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(tempDir);

        var baseName     = Path.GetFileNameWithoutExtension(sourcePath ?? "template");
        var assemblyPath = Path.Combine(tempDir, baseName + ".dll");
        var pdbPath      = Path.Combine(tempDir, baseName + ".pdb");

        var emitOptions = new EmitOptions(
            debugInformationFormat: DebugInformationFormat.PortablePdb,
            pdbFilePath: pdbPath);

        // Embed the template source text directly in the PDB. This means the
        // debugger reads source from the PDB rather than looking for a file on
        // disk by path, eliminating any path-casing or URI mismatch issues.
        var syntaxTree   = compilation.SyntaxTrees.First();
        var embeddedText = EmbeddedText.FromSource(
            syntaxTree.FilePath,
            syntaxTree.GetText());

        // Emit into a closed scope so both FileStreams are fully disposed
        // before LoadFromAssemblyPath opens the file — an open write handle
        // causes a "file in use" error on Windows.
        EmitResult emitResult;
        using (var peStream  = new FileStream(assemblyPath, FileMode.Create, FileAccess.Write))
        using (var pdbStream = new FileStream(pdbPath,      FileMode.Create, FileAccess.Write))
        {
            emitResult = compilation.Emit(peStream, pdbStream,
                options: emitOptions,
                embeddedTexts: [embeddedText]);
        }

        HandleDiagnostics(emitResult, sourcePath);

        return loadContext.LoadFromAssemblyPath(assemblyPath);
    }

    /// <summary>
    /// Emits the compiled assembly and PDB to in-memory streams.
    /// Used for normal (non-debug) runs where disk I/O and temp file cleanup
    /// are undesirable.
    /// </summary>
    private static Assembly EmitToMemory(
        CSharpCompilation compilation,
        string? sourcePath,
        TemplateAssemblyLoadContext loadContext)
    {
        using var peStream  = new MemoryStream();
        using var pdbStream = new MemoryStream();

        var emitResult = compilation.Emit(peStream, pdbStream);

        HandleDiagnostics(emitResult, sourcePath);

        peStream.Seek(0, SeekOrigin.Begin);
        pdbStream.Seek(0, SeekOrigin.Begin);

        return loadContext.LoadFromStream(peStream, pdbStream);
    }

    /// <summary>
    /// Inspects the emit result and throws <see cref="TemplateCompilationException"/>
    /// if there are any errors. Shared between disk and in-memory paths.
    /// </summary>
    private static void HandleDiagnostics(EmitResult emitResult, string? sourcePath)
    {
        if (emitResult.Success) return;

        var diagnostics = emitResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => new TemplateDiagnostic(
                Severity: DiagnosticSeverityLevel.Error,
                Message:  d.GetMessage(),
                FilePath: d.Location.SourceTree?.FilePath,
                Line:     d.Location.GetLineSpan().StartLinePosition.Line     + 1,
                Column:   d.Location.GetLineSpan().StartLinePosition.Character + 1))
            .ToList();

        throw new TemplateCompilationException(
            $"Template compilation failed with {diagnostics.Count} error(s).", diagnostics);
    }

    // -- Type discovery --------------------------------------------------------

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

    // -- Path normalisation ---------------------------------------------------

    /// <summary>
    /// Returns the path with filesystem-accurate casing by round-tripping
    /// through <see cref="FileInfo"/>. On Windows and macOS, where the
    /// filesystem is case-insensitive but case-preserving, this ensures the
    /// path embedded in the PDB matches the URI VS Code uses for the open
    /// document, which is required for breakpoints to bind correctly.
    /// Returns <paramref name="path"/> unchanged if the file does not exist
    /// or <paramref name="path"/> is null.
    /// </summary>
    private static string? NormalisePathCasing(string? path)
    {
        if (path == null) return null;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return path;
            // FullName on an existing FileInfo reflects the actual on-disk casing
            return info.FullName;
        }
        catch
        {
            return path;
        }
    }

    // -- References ------------------------------------------------------------

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

        // Fall back to the default context (handles BCL, HoofMark.CSharpTemplating.Abstractions, etc.)
        // Returning null tells the runtime to continue with normal resolution
        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        // Strip extension if present - callers may pass "SNI" or "SNI.dll"
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
