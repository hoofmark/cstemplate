namespace HoofMark.CSharpTemplating.Abstractions;

/// <summary>
/// Marker interface for template classes. Implement this in your template file
/// and the engine will discover and execute it.
/// </summary>
/// <example>
/// <code>
/// public class MyTemplate : ITemplate
/// {
///     public static void Run(ITemplateContext context)
///     {
///         var ns = context.Config.Get("Namespace");
///         context.WriteFile("Output.cs", $"namespace {ns} {{ }}");
///     }
/// }
/// </code>
/// </example>
public interface ITemplate
{
    static abstract void Run(ITemplateContext context);
}
