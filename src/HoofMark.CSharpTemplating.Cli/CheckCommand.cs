using System.CommandLine;
using HoofMark.CSharpTemplating.Core;

namespace HoofMark.CSharpTemplating.Cli;

/// <summary>
/// Builds the <c>cstemplate check</c> command.
/// Compiles a template and reports diagnostics without executing it.
/// Primarily used by the VS Code extension to surface compile errors as squiggles.
///
/// Usage:
///   cstemplate check &lt;template&gt; [--json]
/// </summary>
internal static class CheckCommandFactory
{
    public static Command Build()
    {
        var templateArg = new Argument<FileInfo>(
            name: "template",
            description: "Path to the template .cs file to check.")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var jsonOption = new Option<bool>(
            aliases: ["--json"],
            description: "Emit structured JSON output.");

        var command = new Command("check", "Compile a template and report errors without running it.")
        {
            templateArg,
            jsonOption
        };

        command.SetHandler(async (template, json) =>
        {
            var reporter = json
                ? (IOutputReporter)new JsonOutputReporter()
                : new ConsoleOutputReporter();

            await HandleAsync(template, reporter);
        },
        templateArg, jsonOption);

        return command;
    }

    private static Task HandleAsync(FileInfo template, IOutputReporter reporter)
    {
        if (!template.Exists)
        {
            reporter.Error($"Template file not found: '{template.FullName}'");
            Environment.Exit(ExitCodes.InputError);
            return Task.CompletedTask;
        }

        var workspaceConfig = WorkspaceConfig.FindAndLoad(template.DirectoryName!);

        var references = RunCommandFactory.ResolveReferences(
            workspaceConfig, template.DirectoryName!, reporter);

        if (references == null)
            return Task.CompletedTask; // error already reported

        var compiler = new TemplateCompiler(references.ManagedPaths, references.NativePaths);

        try
        {
            using var compiled = compiler.Compile(template.FullName);
            reporter.CheckSuccess(compiled.TemplateName);
            Environment.Exit(ExitCodes.Success);
        }
        catch (TemplateCompilationException ex)
        {
            reporter.CompilationFailure(ex);
            Environment.Exit(ExitCodes.CompilationError);
        }
        catch (Exception ex)
        {
            reporter.Error($"Unexpected error: {ex.Message}");
            Environment.Exit(ExitCodes.UnexpectedError);
        }

        return Task.CompletedTask;
    }
}
