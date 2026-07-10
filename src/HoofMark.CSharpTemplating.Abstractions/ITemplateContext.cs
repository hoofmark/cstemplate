namespace HoofMark.CSharpTemplating.Abstractions;

/// <summary>
/// The context passed to a template's Run method. Provides access to configuration
/// and the ability to write output files.
/// </summary>
public interface ITemplateContext
{
    /// <summary>
    /// Configuration values loaded from the sibling .json file (e.g. MyTemplate.json).
    /// If no config file exists, this is an empty/no-op implementation — all TryGet
    /// calls return false and Get calls return null or default.
    /// </summary>
    ITemplateConfig Config { get; }

    /// <summary>
    /// Write a file with the given content to the output root.
    /// The path is relative to the configured output root directory.
    /// Intermediate directories are created automatically.
    /// </summary>
    /// <param name="relativePath">
    /// Relative path of the file to write, e.g. "Models/Order.cs" or "index.html".
    /// Forward slashes are normalised to the platform separator automatically.
    /// </param>
    /// <param name="content">The full text content of the file.</param>
    void WriteFile(string relativePath, string content);

    /// <summary>
    /// Write a file by building its content with an <see cref="IOutputWriter"/>.
    /// Useful for code generation where indentation tracking is helpful.
    /// </summary>
    /// <param name="relativePath">Relative path of the file to write.</param>
    /// <param name="write">
    /// An action that receives an <see cref="IOutputWriter"/> and writes content to it.
    /// </param>
    void WriteFile(string relativePath, Action<IOutputWriter> write);

    /// <summary>
    /// Reads a file and returns its content as a string.
    /// </summary>
    /// <param name="path">
    /// Path to the file to read. If relative, it is resolved against the following
    /// locations in order, using the first match found:
    /// <list type="number">
    ///   <item>The directory containing the template file</item>
    ///   <item>The project root (from <c>cstemplate.config.json</c>, if configured)</item>
    ///   <item>The output root directory</item>
    /// </list>
    /// If absolute, the path is used directly.
    /// </param>
    /// <returns>The full text content of the file.</returns>
    /// <exception cref="FileNotFoundException">
    /// Thrown if the file cannot be found at any of the resolved locations.
    /// </exception>
    string ReadFile(string path);
}
