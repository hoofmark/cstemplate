namespace HoofMark.CSharpTemplating.Abstractions;

/// <summary>
/// A fluent, indentation-aware writer for building text file content.
/// Returned by <see cref="ITemplateContext.WriteFile(string, Action{IOutputWriter})"/>.
/// </summary>
/// <remarks>
/// All methods return <c>this</c> to allow fluent chaining:
/// <code>
/// context.WriteFile("Order.cs", w => w
///     .WriteLine("namespace MyApp")
///     .WriteLine("{")
///     .Indent()
///         .WriteLine("public class Order")
///         .WriteLine("{")
///         .Indent()
///             .WriteLine("public int Id { get; set; }")
///         .Dedent()
///         .WriteLine("}")
///     .Dedent()
///     .WriteLine("}"));
/// </code>
/// Or using <see cref="Block"/> for automatic brace + indentation management:
/// <code>
/// context.WriteFile("Order.cs", w => w
///     .WriteLine("namespace MyApp")
///     .Block("public class Order", body => body
///         .WriteLine("public int Id { get; set; }")
///         .WriteLine("public decimal Total { get; set; }")));
/// </code>
/// </remarks>
public interface IOutputWriter
{
    /// <summary>
    /// Writes text at the current position without a trailing newline.
    /// If this is the first content on a new line, the current indentation is prepended.
    /// </summary>
    IOutputWriter Write(string text);

    /// <summary>
    /// Writes text followed by a newline.
    /// If <paramref name="text"/> is empty (the default), writes a blank line.
    /// The current indentation is prepended if this is the start of a line.
    /// </summary>
    IOutputWriter WriteLine(string text = "");

    /// <summary>
    /// Increases the indentation level by <paramref name="levels"/> (default 1).
    /// Each level adds one indentation unit (4 spaces by default; configurable via engine settings).
    /// </summary>
    IOutputWriter Indent(int levels = 1);

    /// <summary>
    /// Decreases the indentation level by <paramref name="levels"/> (default 1).
    /// Indentation will not go below zero.
    /// </summary>
    IOutputWriter Dedent(int levels = 1);

    /// <summary>
    /// Writes a block: the header line, an opening brace, an indented body, and a closing brace.
    /// </summary>
    /// <param name="header">The line before the opening brace, e.g. <c>"public class Order"</c>.</param>
    /// <param name="body">An action that writes the indented body content.</param>
    /// <param name="trailingSemicolon">
    /// If <c>true</c>, appends a semicolon after the closing brace (useful for namespace-scoped blocks
    /// in some languages, or struct initialisers).
    /// </param>
    IOutputWriter Block(string header, Action<IOutputWriter> body, bool trailingSemicolon = false);

    /// <summary>
    /// Current indentation level (zero-based number of indent units).
    /// </summary>
    int IndentLevel { get; }

    /// <summary>
    /// Returns the full text that has been written so far.
    /// </summary>
    string ToString();
}
