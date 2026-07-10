namespace HoofMark.CSharpTemplating.Cli;

/// <summary>
/// Process exit codes returned by the CLI.
/// The VS Code extension uses these to distinguish error types without
/// needing to parse output text.
/// </summary>
internal static class ExitCodes
{
    /// <summary>Template ran successfully and all files were written.</summary>
    public const int Success = 0;

    /// <summary>The template failed to compile (Roslyn errors).</summary>
    public const int CompilationError = 1;

    /// <summary>The template compiled but threw during execution.</summary>
    public const int ExecutionError = 2;

    /// <summary>Bad input — missing file, malformed config, etc.</summary>
    public const int InputError = 3;

    /// <summary>An unexpected/unhandled exception occurred.</summary>
    public const int UnexpectedError = 99;
}
