using System.CommandLine;
using System.Diagnostics;
using System.CommandLine.Parsing;
using System.Runtime.InteropServices;
using System.Text.Json;
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
        var templateArg = new Argument<FileInfo>("template")
        {
            Description = "Path to the template .cs file to run.",
            Arity = ArgumentArity.ExactlyOne
        };

        var outputOption = new Option<DirectoryInfo?>("--output", new[] { "-o" })
        {
            Description = "Output root directory for generated files. " +
                          "Defaults to a 'generated/' folder next to the template."
        };

        var configOption = new Option<FileInfo?>("--config", new[] { "-c" })
        {
            Description = "Path to a JSON config file. " +
                          "Defaults to the sibling .json file next to the template."
        };

        var jsonOption = new Option<bool>("--json")
        {
            Description = "Emit structured JSON output (for editor integrations)."
        };

        var debugOption = new Option<bool>("--debug")
        {
            Description = "Enable debug mode (emit compiled assembly and PDB to disk)."
        };

        var command = new Command("run", "Compile and run a template file.");
        command.Add(templateArg);
        command.Add(outputOption);
        command.Add(configOption);
        command.Add(jsonOption);
        command.Add(debugOption);

        command.SetAction(parseResult =>
        {
            var template = parseResult.GetValue(templateArg);
            var output = parseResult.GetValue(outputOption);
            var config = parseResult.GetValue(configOption);
            var json = parseResult.GetValue(jsonOption);
            var debug = parseResult.GetValue(debugOption);
            var reporter = json
                ? (IOutputReporter)new JsonOutputReporter()
                : new ConsoleOutputReporter();

            return HandleAsync(template!, output, config, reporter, debug);
        });

        return command;
    }

    private static async Task HandleAsync(
        FileInfo template,
        DirectoryInfo? output,
        FileInfo? config,
        IOutputReporter reporter,
        bool debug = false)
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

        // --debug: emit PID as JSON so the editor can attach its own debugger,
        // then spin-wait until a debugger connects. Using a spin-wait rather than
        // Debugger.Launch() avoids the Windows JIT debugger dialog which would
        // otherwise offer all registered debuggers (including Visual Studio).
        if (debug)
        {
            var pid = Environment.ProcessId;
            Console.WriteLine(JsonSerializer.Serialize(new
            {
                status  = "waitingForDebugger",
                pid,
                message = $"tgen process {pid} waiting for debugger to attach…"
            }));

            var timeout = TimeSpan.FromSeconds(30);
            var started = DateTime.UtcNow;
            while (!Debugger.IsAttached)
            {
                if (DateTime.UtcNow - started > timeout)
                {
                    Console.Error.WriteLine(
                        "cstemplate: Timed out waiting for debugger after 30s. Continuing without.");
                    break;
                }
                Thread.Sleep(100);
            }

            // Enable these lines to pause at a known location to set breakpoints within the tool code before
            // template code begins executing
            //if (Debugger.IsAttached)
            //    Debugger.Break();
        }
        // Execution continues — TemplateRunner will call ITemplate.Debug(context)
        // which breaks inside the template assembly itself, giving the debugger
        // a chance to bind breakpoints in the user's .template.cs file.

        var runnerOptions = new TemplateRunnerOptions
        {
            AdditionalReferencePaths = references.ManagedPaths,
            NativeLibraryPaths       = references.NativePaths,
            ProjectRoot              = workspaceConfig?.ConfigDirectory,
            DebugMode                = debug
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
