using System.Text.Json;
using HoofMark.CSharpTemplating.Abstractions;

namespace HoofMark.CSharpTemplating.Core;

/// <summary>
/// Loads and provides access to a template's sibling JSON config file.
/// </summary>
internal sealed class TemplateConfig : ITemplateConfig
{
    private readonly IReadOnlyDictionary<string, JsonElement> _values;

    private TemplateConfig(IReadOnlyDictionary<string, JsonElement> values)
    {
        _values = values;
    }

    /// <summary>
    /// Loads config from a JSON file. Returns an empty config if the file does not exist.
    /// The config file is looked up by stripping the template suffix from the file name.
    /// For example, "MyTemplate.template.cs" looks for "MyTemplate.json".
    /// </summary>
    public static TemplateConfig LoadFrom(string templateFilePath)
    {
        var configPath = GetConfigPath(templateFilePath);

        if (!File.Exists(configPath))
            return Empty;

        var json = File.ReadAllText(configPath);
        return Parse(json, configPath);
    }

    /// <summary>
    /// Resolves the sibling config path for a given template file path.
    /// First checks for a sibling JSON file with the same base name (e.g. MyTemplate.template.cs → MyTemplate.template.json).
    /// Strips compound extensions like ".template.cs" down to the base name,
    /// then appends ".json". Falls back to simple extension replacement if
    /// no known compound extension is matched, or if the resulting path does not exist.
    /// </summary>
    internal static string GetConfigPath(string templateFilePath)
    {
        var directory = Path.GetDirectoryName(templateFilePath) ?? "";
        var fileName  = Path.GetFileName(templateFilePath);

        // First, try to find a sibling JSON file with the same base name (e.g. MyTemplate.template.cs → MyTemplate.template.json)
        var configPath = Path.Combine(directory, Path.ChangeExtension(fileName, ".json"));

        // Strip known compound extensions
		if (!File.Exists(configPath))
		{
	        foreach (var compound in new[] { ".template.cs", ".tmpl.cs" })
	        {
	            if (fileName.EndsWith(compound, StringComparison.OrdinalIgnoreCase))
	            {
	                var baseName = fileName[..^compound.Length];
	                configPath = Path.Combine(directory, baseName + ".json");
					break;
	            }
	        }
		}

        // Fall back to simple extension replacement (e.g. MyTemplate.cs → MyTemplate.json)
        return configPath;
    }

    /// <summary>
    /// Parses config from a JSON string. Useful for testing.
    /// </summary>
    public static TemplateConfig Parse(string json, string? sourcePath = null)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                throw new TemplateConfigException(
                    $"Config must be a JSON object at the root level.{(sourcePath != null ? $" File: {sourcePath}" : "")}");

            // Clone all elements so we can dispose the document
            var values = doc.RootElement
                .EnumerateObject()
                .ToDictionary(
                    p => p.Name,
                    p => p.Value.Clone(),
                    StringComparer.OrdinalIgnoreCase);

            return new TemplateConfig(values);
        }
        catch (JsonException ex)
        {
            throw new TemplateConfigException(
                $"Failed to parse config JSON.{(sourcePath != null ? $" File: {sourcePath}" : "")} {ex.Message}", ex);
        }
    }

    /// <summary>An empty config (no sibling JSON file present).</summary>
    public static readonly TemplateConfig Empty =
        new(new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase));

    // --- ITemplateConfig ---

    public IReadOnlyDictionary<string, JsonElement> All => _values;

    public string? Get(string key)
    {
        if (!_values.TryGetValue(key, out var element))
            return null;

        return element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.ToString();
    }

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public T? Get<T>(string key)
    {
        if (!_values.TryGetValue(key, out var element))
            return default;

        return JsonSerializer.Deserialize<T>(element.GetRawText(), DeserializeOptions);
    }

    public bool TryGet(string key, out string? value)
    {
        if (!_values.TryGetValue(key, out var element))
        {
            value = null;
            return false;
        }

        value = element.ValueKind == JsonValueKind.String
            ? element.GetString()
            : element.ToString();

        return true;
    }
}
