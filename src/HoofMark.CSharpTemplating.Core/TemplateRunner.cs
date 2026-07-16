using System.Text;

// Included to support reference in document comments, but not otherwise used in this file.
using HoofMark.CSharpTemplating.Abstractions;

namespace HoofMark.CSharpTemplating.Core;

/// <summary>
/// Orchestrates the full lifecycle of running a template:
/// load config → compile → execute → flush output files to disk.
/// This is the primary entry point for the CLI and the VS Code extension.
/// </summary>
public sealed class TemplateRunner
{
    private readonly TemplateCompiler _compiler;
    private readonly string? _projectRoot;
    private readonly string _indentUnit;
    private readonly bool _debugMode;

    public TemplateRunner(TemplateRunnerOptions? options = null)
    {
        var opts = options ?? new TemplateRunnerOptions();
        _compiler    = new TemplateCompiler(opts.AdditionalReferencePaths, opts.NativeLibraryPaths);
        _projectRoot = opts.ProjectRoot;
        _indentUnit  = opts.IndentUnit;
        _debugMode   = opts.DebugMode;
    }

    /// <summary>
    /// Runs a template file end-to-end.
    /// </summary>
    /// <param name="templateFilePath">Absolute or relative path to the <c>.cs</c> template file.</param>
    /// <param name="outputRoot">
    /// Root directory for generated files. If <c>null</c>, defaults to a <c>generated/</c>
    /// folder adjacent to the template file.
    /// </param>
    /// <param name="configOverridePath">
    /// Path to a JSON config file to use instead of the default sibling file.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="TemplateRunResult"/> describing what was generated.</returns>
    public async Task<TemplateRunResult> RunAsync(
        string templateFilePath,
        string? outputRoot = null,
        string? configOverridePath = null,
        CancellationToken cancellationToken = default)
    {
        templateFilePath = Path.GetFullPath(templateFilePath);

        var resolvedOutputRoot = outputRoot != null
            ? Path.GetFullPath(outputRoot)
            : Path.Combine(Path.GetDirectoryName(templateFilePath)!, "generated");

        // 1. Load config
        var configPath = configOverridePath ?? templateFilePath;
        var config = TemplateConfig.LoadFrom(configPath);

        // 2. Compile
        using var compiled = _compiler.Compile(templateFilePath, Encoding.UTF8, _debugMode);

        // 3. Build context
        var templateDirectory = Path.GetDirectoryName(templateFilePath)!;
        var context = new TemplateContext(
            config,
            resolvedOutputRoot,
            templateDirectory,
            _projectRoot,
            _indentUnit);

        // 4. Execute
        compiled.Run(context);

        cancellationToken.ThrowIfCancellationRequested();

        // 5. Flush to disk
        var writtenFiles = await context.FlushAsync(cancellationToken);

        return new TemplateRunResult(
            TemplateName: compiled.TemplateName,
            TemplateFilePath: templateFilePath,
            OutputRoot: resolvedOutputRoot,
            GeneratedFiles: writtenFiles);
    }
}

/// <summary>
/// Options for configuring a <see cref="TemplateRunner"/>.
/// </summary>
public sealed class TemplateRunnerOptions
{
    /// <summary>
    /// Additional assembly reference paths to include in Roslyn compilation.
    /// These are typically resolved from <c>cstemplate.config.json</c>.
    /// </summary>
    public IEnumerable<string>? AdditionalReferencePaths { get; init; }

    /// <summary>
    /// The indentation unit used by <see cref="IOutputWriter"/> implementations.
    /// Defaults to 4 spaces.
    /// </summary>
    public string IndentUnit { get; init; } = "    ";

    /// <summary>
    /// Paths to native libraries to make available to templates via P/Invoke.
    /// Typically populated from NuGet packages that ship native dependencies
    /// (e.g. Microsoft.Data.SqlClient ships Microsoft.Data.SqlClient.SNI.dll).
    /// </summary>
    public IEnumerable<string>? NativeLibraryPaths { get; init; }

    /// <summary>
    /// The project root directory, typically the folder containing <c>cstemplate.config.json</c>.
    /// Used as a search location when resolving relative paths in <c>ReadFile</c>.
    /// If <c>null</c>, only the template directory and output root are searched.
    /// </summary>
    public string? ProjectRoot { get; init; }

    /// <summary>
    /// When <c>true</c>, the compiler emits the template assembly and PDB to a
    /// temporary directory on disk rather than to memory streams. This allows an
    /// attached debugger to locate the symbols and map execution back to the
    /// original <c>.template.cs</c> source file, enabling breakpoints inside
    /// template code. Has no effect when no debugger is attached.
    /// </summary>
    public bool DebugMode { get; init; }
}

/// <summary>
/// The result of a successful template run.
/// </summary>
public sealed record TemplateRunResult(
    string TemplateName,
    string TemplateFilePath,
    string OutputRoot,
    IReadOnlyList<string> GeneratedFiles)
{
    /// <summary>How many files were written to disk.</summary>
    public int FileCount => GeneratedFiles.Count;

    public override string ToString() =>
        $"Template '{TemplateName}' generated {FileCount} file(s) in '{OutputRoot}'.";
}
