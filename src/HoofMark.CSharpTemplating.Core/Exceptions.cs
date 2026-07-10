namespace HoofMark.CSharpTemplating.Core;

// ---------------------------------------------------------------------------
// Diagnostics
// ---------------------------------------------------------------------------

/// <summary>Severity level of a template diagnostic message.</summary>
public enum DiagnosticSeverityLevel { Info, Warning, Error }

/// <summary>
/// A single diagnostic produced during template compilation.
/// Maps directly to a Roslyn <c>Diagnostic</c>.
/// </summary>
public sealed record TemplateDiagnostic(
    DiagnosticSeverityLevel Severity,
    string Message,
    string? FilePath,
    int Line,
    int Column)
{
    public override string ToString() =>
        FilePath != null
            ? $"{FilePath}({Line},{Column}): {Severity.ToString().ToLower()} - {Message}"
            : $"({Line},{Column}): {Severity.ToString().ToLower()} - {Message}";
}

// ---------------------------------------------------------------------------
// Exceptions
// ---------------------------------------------------------------------------

/// <summary>Base class for all TemplateEngine exceptions.</summary>
public abstract class CsTemplateException : Exception
{
    protected CsTemplateException(string message) : base(message) { }
    protected CsTemplateException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when a template's sibling JSON config file cannot be found or parsed.
/// </summary>
public sealed class TemplateConfigException : CsTemplateException
{
    public TemplateConfigException(string message) : base(message) { }
    public TemplateConfigException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// Thrown when Roslyn fails to compile a template.
/// Contains the full list of <see cref="TemplateDiagnostic"/>s.
/// </summary>
public sealed class TemplateCompilationException : CsTemplateException
{
    public IReadOnlyList<TemplateDiagnostic> Diagnostics { get; }

    public TemplateCompilationException(string message, IReadOnlyList<TemplateDiagnostic> diagnostics)
        : base(message)
    {
        Diagnostics = diagnostics;
    }

    public override string ToString()
    {
        if (Diagnostics.Count == 0)
            return Message;

        var lines = Diagnostics.Select(d => $"  {d}");
        return $"{Message}\n{string.Join("\n", lines)}";
    }
}

/// <summary>
/// Thrown when a template throws an unhandled exception during <c>Run()</c>,
/// or when it attempts an illegal operation (e.g. path traversal in WriteFile).
/// </summary>
public sealed class TemplateExecutionException : CsTemplateException
{
    /// <summary>The name of the template class that threw the exception.</summary>
    public string? TemplateName { get; init; }

    public TemplateExecutionException(string message) : base(message) { }
    public TemplateExecutionException(string message, Exception inner) : base(message, inner) { }
}
