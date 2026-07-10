using System.Reflection;
using System.Runtime.Loader;
using HoofMark.CSharpTemplating.Abstractions;

namespace HoofMark.CSharpTemplating.Core;

/// <summary>
/// Holds a compiled template type and its associated <see cref="AssemblyLoadContext"/>.
/// Invoke <see cref="Run"/> to execute the template.
/// Dispose to unload the assembly from memory.
/// </summary>
public sealed class CompiledTemplate : IDisposable
{
    private readonly Type _templateType;
    private readonly AssemblyLoadContext _loadContext;
    private bool _disposed;

    // Cached MethodInfo for the generic shim, looked up once per instance
    private static readonly MethodInfo InvokeShimMethod =
        typeof(CompiledTemplate)
            .GetMethod(nameof(InvokeShim), BindingFlags.Static | BindingFlags.NonPublic)!;

    internal CompiledTemplate(Type templateType, AssemblyLoadContext loadContext)
    {
        _templateType = templateType;
        _loadContext = loadContext;
    }

    /// <summary>The name of the template class.</summary>
    public string TemplateName => _templateType.Name;

    /// <summary>
    /// Executes the template's <c>Run</c> method with the provided context.
    /// </summary>
    /// <exception cref="TemplateExecutionException">
    /// Thrown if the template's Run method throws an unhandled exception.
    /// </exception>
    public void Run(ITemplateContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        try
        {
            // We cannot call a static abstract interface method directly via reflection
            // on an unknown type. The standard workaround is a generic shim method
            // constrained to ITemplate, called via MakeGenericMethod.
            //
            //   static void InvokeShim<T>(ITemplateContext ctx) where T : ITemplate
            //       => T.Run(ctx);
            //
            InvokeShimMethod
                .MakeGenericMethod(_templateType)
                .Invoke(null, [context]);
        }
        catch (TargetInvocationException tie) when (tie.InnerException != null)
        {
            // Unwrap the reflection wrapper to surface the real exception
            throw new TemplateExecutionException(
                $"Template '{TemplateName}' threw an exception during execution.",
                tie.InnerException)
            {
                TemplateName = TemplateName
            };
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _loadContext.Unload();
    }

    // The generic shim. Must be non-generic at the declaring type level so
    // MakeGenericMethod works. The constraint is what makes T.Run(ctx) legal.
    private static void InvokeShim<T>(ITemplateContext ctx) where T : ITemplate
        => T.Run(ctx);
}
