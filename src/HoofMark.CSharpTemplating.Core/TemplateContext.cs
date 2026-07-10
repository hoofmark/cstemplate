using HoofMark.CSharpTemplating.Abstractions;

namespace HoofMark.CSharpTemplating.Core;

/// <summary>
/// Concrete implementation of <see cref="ITemplateContext"/>.
/// Collects all <c>WriteFile</c> calls during template execution, then flushes
/// them to disk via <see cref="FlushAsync"/>.
/// </summary>
internal sealed class TemplateContext : ITemplateContext
{
    private readonly string _outputRoot;
    private readonly string _templateDirectory;
    private readonly string? _projectRoot;
    private readonly string _indentUnit;
    private readonly List<PendingFile> _pendingFiles = new();

    public TemplateContext(
        ITemplateConfig config,
        string outputRoot,
        string templateDirectory,
        string? projectRoot = null,
        string indentUnit = "    ")
    {
        Config             = config;
        _outputRoot        = outputRoot;
        _templateDirectory = templateDirectory;
        _projectRoot       = projectRoot;
        _indentUnit        = indentUnit;
    }

    public ITemplateConfig Config { get; }

    public void WriteFile(string relativePath, string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);

        // Normalise path separators and guard against path traversal
        var normalisedPath = NormalisePath(relativePath);
        _pendingFiles.Add(new PendingFile(normalisedPath, content));
    }

    public void WriteFile(string relativePath, Action<IOutputWriter> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(write);

        var writer = new OutputWriter(_indentUnit);
        write(writer);
        WriteFile(relativePath, writer.ToString());
    }

    public string ReadFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        // Absolute path — use directly
        if (Path.IsPathRooted(path))
        {
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"ReadFile: file not found at absolute path '{path}'.", path);

            return File.ReadAllText(path);
        }

        // Relative path — search in order: template dir, project root, output root
        var searchRoots = new List<string> { _templateDirectory };
        if (_projectRoot != null)
            searchRoots.Add(_projectRoot);
        searchRoots.Add(_outputRoot);

        foreach (var root in searchRoots)
        {
            var candidate = Path.GetFullPath(Path.Combine(root, path));
            if (File.Exists(candidate))
                return File.ReadAllText(candidate);
        }

        // Build a helpful error listing all locations searched
        var searched = string.Join(Environment.NewLine,
            searchRoots.Select(r => $"  - {Path.GetFullPath(Path.Combine(r, path))}"));

        throw new FileNotFoundException(
            $"ReadFile: '{path}' not found. Locations searched:{Environment.NewLine}{searched}",
            path);
    }

    /// <summary>
    /// Writes all pending files to disk. Called by the engine after <c>Run()</c> completes.
    /// Returns the list of absolute paths that were written.
    /// </summary>
    public async Task<IReadOnlyList<string>> FlushAsync(CancellationToken cancellationToken = default)
    {
        var written = new List<string>(_pendingFiles.Count);

        foreach (var file in _pendingFiles)
        {
            var absolutePath = Path.GetFullPath(Path.Combine(_outputRoot, file.RelativePath));

            // Safety: ensure the resolved path is still inside the output root
            var root = Path.GetFullPath(_outputRoot);
            if (!absolutePath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new TemplateExecutionException(
                    $"Template attempted to write outside the output root: '{file.RelativePath}'");

            var directory = Path.GetDirectoryName(absolutePath)!;
            Directory.CreateDirectory(directory);

            await File.WriteAllTextAsync(absolutePath, file.Content, cancellationToken);
            written.Add(absolutePath);
        }

        return written;
    }

    /// <summary>Files queued for writing, exposed for diagnostics/testing.</summary>
    public IReadOnlyList<PendingFile> PendingFiles => _pendingFiles;

    private static string NormalisePath(string relativePath)
    {
        // Normalise slashes
        var path = relativePath.Replace('/', Path.DirectorySeparatorChar)
                               .Replace('\\', Path.DirectorySeparatorChar);

        // Reject absolute paths and traversal attempts — the engine catches this
        // again after combining with the root, but fail fast here too
        if (Path.IsPathRooted(path))
            throw new TemplateExecutionException(
                $"WriteFile path must be relative, not absolute: '{relativePath}'");

        if (path.Split(Path.DirectorySeparatorChar).Any(s => s == ".."))
            throw new TemplateExecutionException(
                $"WriteFile path must not contain '..': '{relativePath}'");

        return path;
    }

    /// <summary>A file queued to be written to disk.</summary>
    public sealed record PendingFile(string RelativePath, string Content);
}
