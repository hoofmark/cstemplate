using System.Text.Json;
using System.Text.Json.Serialization;

namespace HoofMark.CSharpTemplating.Cli;

/// <summary>
/// Represents the optional workspace-level <c>cstemplate.config.json</c> file.
/// The engine searches for this file by walking up the directory tree from
/// the template file, stopping at the first match or at the filesystem root.
/// </summary>
internal sealed class WorkspaceConfig
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    [JsonPropertyName("outputRoot")]
    public string? OutputRoot { get; init; }

    [JsonPropertyName("references")]
    public string[]? References { get; init; }

    /// <summary>
    /// Path to the template project's .csproj file, relative to this config file.
    /// Used to locate <c>obj/project.assets.json</c> for NuGet package resolution.
    /// </summary>
    [JsonPropertyName("project")]
    public string? Project { get; init; }

    /// <summary>
    /// NuGet package IDs to resolve from the project's restored assets.
    /// Only these packages (and their transitive dependencies) will be passed
    /// to the compiler. Versions are taken from the project's restored assets —
    /// no need to specify them here.
    /// </summary>
    [JsonPropertyName("nugetPackages")]
    public string[]? NuGetPackages { get; init; }

    /// <summary>The directory this config file was found in.</summary>
    [JsonIgnore]
    public string? ConfigDirectory { get; private set; }

    /// <summary>
    /// Searches upward from <paramref name="startDirectory"/> for a <c>cstemplate.config.json</c>.
    /// Returns <c>null</c> if none is found.
    /// </summary>
    public static WorkspaceConfig? FindAndLoad(string startDirectory)
    {
        var dir = new DirectoryInfo(startDirectory);

        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "cstemplate.config.json");

            if (File.Exists(candidate))
            {
                try
                {
                    var json = File.ReadAllText(candidate);
                    var config = JsonSerializer.Deserialize<WorkspaceConfig>(json, SerializerOptions);

                    if (config != null)
                    {
                        config.ConfigDirectory = dir.FullName;
                        return config;
                    }
                }
                catch (JsonException ex)
                {
                    Console.Error.WriteLine(
                        $"Warning: Failed to parse '{candidate}': {ex.Message}. Workspace config will be ignored.");
                    return null;
                }
            }

            dir = dir.Parent;
        }

        return null;
    }

    /// <summary>
    /// Resolves the output root path relative to the config file's directory.
    /// Returns <c>null</c> if no output root is configured.
    /// </summary>
    public string? ResolvedOutputRoot(string templateDirectory)
    {
        if (string.IsNullOrWhiteSpace(OutputRoot))
            return null;

        var baseDir = ConfigDirectory ?? templateDirectory;
        return Path.GetFullPath(OutputRoot, baseDir);
    }

    /// <summary>
    /// Resolves local reference paths relative to the config file's directory.
    /// </summary>
    public IEnumerable<string> ResolvedReferencePaths(string templateDirectory)
    {
        if (References == null || References.Length == 0)
            yield break;

        var baseDir = ConfigDirectory ?? templateDirectory;

        foreach (var reference in References)
            yield return Path.GetFullPath(reference, baseDir);
    }

    /// <summary>
    /// Resolves the path to <c>obj/project.assets.json</c> for the configured project.
    /// Returns <c>null</c> if no project is configured.
    /// </summary>
    public string? ResolvedAssetsFilePath(string templateDirectory)
    {
        if (string.IsNullOrWhiteSpace(Project))
            return null;

        var baseDir    = ConfigDirectory ?? templateDirectory;
        var projectDir = Path.GetDirectoryName(Path.GetFullPath(Project, baseDir))!;
        return Path.Combine(projectDir, "obj", "project.assets.json");
    }
}
