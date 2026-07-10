using System.Text.Json;

namespace HoofMark.CSharpTemplating.Abstractions;

/// <summary>
/// Provides access to configuration values loaded from the template's sibling JSON file.
/// </summary>
/// <remarks>
/// Given a template file <c>MyTemplate.cs</c>, the engine looks for <c>MyTemplate.json</c>
/// in the same directory. The JSON file should be a flat or nested object:
/// <code>
/// {
///   "Namespace": "MyApp.Models",
///   "ClassName": "OrderService",
///   "Properties": [
///     { "Name": "Id", "Type": "int" }
///   ]
/// }
/// </code>
/// </remarks>
public interface ITemplateConfig
{
    /// <summary>
    /// Gets a top-level string value by key.
    /// Returns <c>null</c> if the key does not exist.
    /// </summary>
    string? Get(string key);

    /// <summary>
    /// Gets and deserialises a value by key.
    /// Returns <c>default</c> if the key does not exist.
    /// </summary>
    /// <typeparam name="T">The type to deserialise the value into.</typeparam>
    T? Get<T>(string key);

    /// <summary>
    /// Tries to get a top-level string value by key.
    /// </summary>
    /// <returns><c>true</c> if the key exists; otherwise <c>false</c>.</returns>
    bool TryGet(string key, out string? value);

    /// <summary>
    /// Raw access to all top-level values as <see cref="JsonElement"/>s.
    /// Useful for iterating all keys or accessing deeply nested structures.
    /// </summary>
    IReadOnlyDictionary<string, JsonElement> All { get; }
}
