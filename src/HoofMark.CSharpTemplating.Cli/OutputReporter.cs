using System.Text.Json;
using System.Text.Json.Serialization;
using HoofMark.CSharpTemplating.Core;

namespace HoofMark.CSharpTemplating.Cli;

// ---------------------------------------------------------------------------
// Reporter interface — decouples command handlers from output format
// ---------------------------------------------------------------------------

internal interface IOutputReporter
{
    void Success(TemplateRunResult result);
    void CheckSuccess(string templateName);
    void CompilationFailure(TemplateCompilationException ex);
    void ExecutionFailure(TemplateExecutionException ex);
    void Error(string message);
}

// ---------------------------------------------------------------------------
// Human-readable console output
// ---------------------------------------------------------------------------

internal sealed class ConsoleOutputReporter : IOutputReporter
{
    public void Success(TemplateRunResult result)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ {result}");
        Console.ResetColor();

        foreach (var file in result.GeneratedFiles)
            Console.WriteLine($"  → {file}");
    }

    public void CheckSuccess(string templateName)
    {
        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"✓ '{templateName}' compiled successfully.");
        Console.ResetColor();
    }

    public void CompilationFailure(TemplateCompilationException ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"✗ Compilation failed ({ex.Diagnostics.Count} error(s)):");
        Console.ResetColor();

        foreach (var d in ex.Diagnostics)
            Console.Error.WriteLine($"  {d}");
    }

    public void ExecutionFailure(TemplateExecutionException ex)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"✗ Template '{ex.TemplateName}' threw an exception during execution.");
        Console.ResetColor();

        var inner = ex.InnerException;
        if (inner != null)
        {
            Console.Error.WriteLine($"  {inner.GetType().FullName}: {inner.Message}");

            // Walk the inner exception chain
            var cause = inner.InnerException;
            while (cause != null)
            {
                Console.Error.WriteLine($"  Caused by: {cause.GetType().FullName}: {cause.Message}");
                cause = cause.InnerException;
            }

            // Print stack trace of the innermost exception
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Error.WriteLine();
            Console.Error.WriteLine("  Stack trace:");
            var stackLines = (inner.StackTrace ?? "  (no stack trace available)")
                .Split(Environment.NewLine);
            foreach (var line in stackLines)
                Console.Error.WriteLine($"  {line.TrimStart()}");
            Console.ResetColor();
        }
    }

    public void Error(string message)
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.Error.WriteLine($"✗ {message}");
        Console.ResetColor();
    }
}

// ---------------------------------------------------------------------------
// Structured JSON output — consumed by the VS Code extension
// ---------------------------------------------------------------------------

/// <summary>
/// Writes a single JSON object to stdout. The VS Code extension parses this
/// to surface diagnostics, reveal generated files, etc.
/// </summary>
internal sealed class JsonOutputReporter : IOutputReporter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public void Success(TemplateRunResult result)
    {
        Write(new CliOutput
        {
            Status = "success",
            TemplateName = result.TemplateName,
            OutputRoot = result.OutputRoot,
            GeneratedFiles = [.. result.GeneratedFiles],
            FileCount = result.FileCount
        });
    }

    public void CheckSuccess(string templateName)
    {
        Write(new CliOutput
        {
            Status = "success",
            TemplateName = templateName
        });
    }

    public void CompilationFailure(TemplateCompilationException ex)
    {
        Write(new CliOutput
        {
            Status = "compilationError",
            Message = ex.Message,
            Diagnostics = ex.Diagnostics
                .Select(d => new CliDiagnostic(d.Severity.ToString().ToLower(), d.Message, d.FilePath, d.Line, d.Column))
                .ToArray()
        });
    }

    public void ExecutionFailure(TemplateExecutionException ex)
    {
        var inner = ex.InnerException;
        Write(new CliOutput
        {
            Status          = "executionError",
            Message         = ex.Message,
            InnerExceptionType    = inner?.GetType().FullName,
            InnerMessage    = inner?.Message,
            StackTrace      = inner?.StackTrace
        });
    }

    public void Error(string message)
    {
        Write(new CliOutput
        {
            Status = "error",
            Message = message
        });
    }

    private static void Write(CliOutput output)
        => Console.WriteLine(JsonSerializer.Serialize(output, SerializerOptions));
}

// ---------------------------------------------------------------------------
// JSON output models
// ---------------------------------------------------------------------------

internal sealed class CliOutput
{
    public required string Status { get; init; }
    public string? TemplateName { get; init; }
    public string? OutputRoot { get; init; }
    public string[]? GeneratedFiles { get; init; }
    public int? FileCount { get; init; }
    public string? Message { get; init; }
    public string? InnerExceptionType { get; init; }
    public string? InnerMessage { get; init; }
    public string? StackTrace { get; init; }
    public CliDiagnostic[]? Diagnostics { get; init; }
}

internal sealed record CliDiagnostic(
    string Severity,
    string Message,
    string? FilePath,
    int Line,
    int Column);
