using System.CommandLine;
using System.Runtime.InteropServices;
using HoofMark.CSharpTemplating.Core;

namespace HoofMark.CSharpTemplating.Cli;

/// <summary>
/// Builds the <c>cstemplate run</c> command.
///
/// Usage:
///   cstemplate run &lt;template&gt; [--output &lt;dir&gt;] [--config &lt;file&gt;] [--json]
/// </summary>
internal static class RunCommandFactory
{
    public static Command Build()
    {
        var templateArg = new Argument<FileInfo>(
            name: "template",
            description: "Path to the template .cs file to run.")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var outputOption = new Option<DirectoryInfo?>(
            aliases: ["--output", "-o"],
            description: "Output root directory for generated files. " +
                         "Defaults to a 'generated/' folder next to the template.");

        var configOption = new Option<FileInfo?>(
            aliases: ["--config", "-c"],
            description: "Path to a JSON config file. " +
                         "Defaults to the sibling .json file next to the template.");

        var jsonOption = new Option<bool>(
            aliases: ["--json"],
            description: "Emit structured JSON output (for editor integrations).");

        var command = new Command("run", "Compile and run a template file.")
        {
            templateArg,
            outputOption,
            configOption,
            jsonOption
        };

        command.SetHandler(async (template, output, config, json) =>
        {
            var reporter = json
                ? (IOutputReporter)new JsonOutputReporter()
                : new ConsoleOutputReporter();

            await HandleAsync(template, output, config, reporter);
        },
        templateArg, outputOption, configOption, jsonOption);

        return command;
    }

    private static async Task HandleAsync(
        FileInfo template,
        DirectoryInfo? output,
        FileInfo? config,
        IOutputReporter reporter)
    {
        if (!template.Exists)
        {
            reporter.Error($"Template file not found: '{template.FullName}'");
            Environment.Exit(ExitCodes.InputError);
            return;
        }

        if (config != null && !config.Exists)
        {
            reporter.Error($"Config file not found: '{config.FullName}'");
            Environment.Exit(ExitCodes.InputError);
            return;
        }

        // Load workspace config (cstemplate.config.json) walking up from template dir
        var workspaceConfig = WorkspaceConfig.FindAndLoad(template.DirectoryName!);

        // Resolve output root: CLI --output arg takes priority, then workspace config, then default
        var resolvedOutputRoot = output?.FullName
            ?? workspaceConfig?.ResolvedOutputRoot(template.DirectoryName!);

        // Resolve reference paths: local assemblies + NuGet packages via project.assets.json
        var references = ResolveReferences(workspaceConfig, template.DirectoryName!, reporter);
        if (references == null) return; // error already reported

        var runnerOptions = new TemplateRunnerOptions
        {
            AdditionalReferencePaths = references.ManagedPaths,
            NativeLibraryPaths       = references.NativePaths,
            ProjectRoot              = workspaceConfig?.ConfigDirectory
        };

        var runner = new TemplateRunner(runnerOptions);

        try
        {
            var result = await runner.RunAsync(
                templateFilePath: template.FullName,
                outputRoot: resolvedOutputRoot,
                configOverridePath: config?.FullName);

            reporter.Success(result);
            Environment.Exit(ExitCodes.Success);
        }
        catch (TemplateCompilationException ex)
        {
            reporter.CompilationFailure(ex);
            Environment.Exit(ExitCodes.CompilationError);
        }
        catch (TemplateExecutionException ex)
        {
            reporter.ExecutionFailure(ex);
            Environment.Exit(ExitCodes.ExecutionError);
        }
        catch (TemplateConfigException ex)
        {
            reporter.Error($"Config error: {ex.Message}");
            Environment.Exit(ExitCodes.InputError);
        }
        catch (Exception ex)
        {
            reporter.Error($"Unexpected error: {ex.Message}");
            Environment.Exit(ExitCodes.UnexpectedError);
        }
    }

    /// <summary>
    /// Resolves all reference paths from local references and NuGet packages.
    /// Returns null if resolution failed (error already reported to reporter).
    /// </summary>
    internal static ResolvedReferences? ResolveReferences(
        WorkspaceConfig? workspaceConfig,
        string templateDirectory,
        IOutputReporter reporter)
    {
        var managedPaths = new List<string>(
            workspaceConfig?.ResolvedReferencePaths(templateDirectory) ?? []);
        var nativePaths = new List<string>();

        if (workspaceConfig?.NuGetPackages?.Length > 0)
        {
            var assetsFile = workspaceConfig.ResolvedAssetsFilePath(templateDirectory);

            if (assetsFile == null)
            {
                reporter.Error(
                    "nugetPackages are configured but no 'project' path is set in cstemplate.config.json. " +
                    "Add a 'project' entry pointing to your .csproj file.");
                Environment.Exit(ExitCodes.InputError);
                return null;
            }

            try
            {
                // Pass the tool's own TFM so ResolveTargetKey can walk the
                // compatibility chain downward (net10.0 → net9.0 → net8.0 …)
                // when the project was restored for an older framework.
                var tfm      = ToolRuntime.GetTargetFramework();
                var resolver = new NuGetResolver();
                var result   = resolver.Resolve(assetsFile, workspaceConfig.NuGetPackages, tfm);
                managedPaths.AddRange(result.ManagedAssemblyPaths);
                nativePaths.AddRange(result.NativeLibraryPaths);
            }
            catch (NuGetResolutionException ex)
            {
                reporter.Error($"NuGet resolution failed: {ex.Message}");
                Environment.Exit(ExitCodes.InputError);
                return null;
            }
        }

        return new ResolvedReferences(managedPaths, nativePaths);
    }
}

/// <summary>
/// Managed and native assembly paths resolved for a template run.
/// </summary>
internal sealed record ResolvedReferences(
    IReadOnlyList<string> ManagedPaths,
    IReadOnlyList<string> NativePaths);

/// <summary>
/// Returns the target framework moniker the tool itself was built for,
/// derived from the runtime version. Used to select the best compatible
/// target when resolving NuGet assets.
/// </summary>
internal static class ToolRuntime
{
    public static string GetTargetFramework()
    {
        // Environment.Version returns the runtime version, e.g. 10.0.1
        // Map it to the matching TFM: net10.0, net9.0, net8.0 etc.
        var v = Environment.Version;
        return $"net{v.Major}.{v.Minor}";
    }
}
