using HoofMark.CSharpTemplating.Core;

namespace HoofMark.CSharpTemplating.Tests.Helpers;

internal static class AssertHelper
{
    /// <summary>
    /// Runs an action expected to succeed, and if a TemplateCompilationException
    /// is thrown, rethrows with the full diagnostic details visible in the test output.
    /// </summary>
    public static void ShouldCompileAndRun(Action action)
    {
        try
        {
            action();
        }
        catch (TemplateCompilationException ex)
        {
            throw new Exception(
                $"Template compilation failed unexpectedly:\n{ex}", ex);
        }
    }

    /// <summary>
    /// Async version of ShouldCompileAndRun.
    /// </summary>
    public static async Task ShouldCompileAndRunAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (TemplateCompilationException ex)
        {
            throw new Exception(
                $"Template compilation failed unexpectedly:\n{ex}", ex);
        }
    }
}
