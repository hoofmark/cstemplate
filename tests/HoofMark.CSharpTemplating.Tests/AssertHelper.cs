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

    public static bool WithMessage(this Exception ex, string expectedMsg)
    {
        //  Allow for wildcard characters
        var msgComponents = expectedMsg.Split('*');
        var matched = msgComponents.All(x => ex.Message.Contains(x));
        matched.ShouldBeTrue();
        return matched;
    }

    public static void ShouldContainSingle<T>(this IEnumerable<T> data)
    => data.Count().ShouldBe(1);

    public static void ShouldHaveCount<T>(this IEnumerable<T> data, int expectedCount)
    => data.Count().ShouldBe(expectedCount);
}
