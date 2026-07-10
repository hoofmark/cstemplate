namespace HoofMark.CSharpTemplating.Tests.Helpers;

/// <summary>
/// Creates a unique temporary directory for a test and deletes it on disposal.
/// Use with <c>using var dir = new TempDirectory();</c>
/// </summary>
internal sealed class TempDirectory : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"cstemplate-tests-{Guid.NewGuid():N}");

    public TempDirectory()
    {
        Directory.CreateDirectory(Path);
    }

    /// <summary>Returns the full path of a file within this temp directory.</summary>
    public string File(string relativePath) =>
        System.IO.Path.Combine(Path, relativePath);

    /// <summary>Writes text to a file inside this temp directory, creating subdirs as needed.</summary>
    public async Task WriteAsync(string relativePath, string content)
    {
        var fullPath = File(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(fullPath)!);
        await System.IO.File.WriteAllTextAsync(fullPath, content);
    }

    /// <summary>Reads a file from within this temp directory.</summary>
    public Task<string> ReadAsync(string relativePath) =>
        System.IO.File.ReadAllTextAsync(File(relativePath));

    /// <summary>Returns true if the file exists within this temp directory.</summary>
    public bool Exists(string relativePath) =>
        System.IO.File.Exists(File(relativePath));

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch { /* best-effort cleanup */ }
    }
}
