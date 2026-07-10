using System.CommandLine;
using HoofMark.CSharpTemplating.Cli;

// Root command: cstemplate
var root = new RootCommand("cstemplate — TemplateEngine CLI");

root.AddCommand(RunCommandFactory.Build());
root.AddCommand(CheckCommandFactory.Build());

return await root.InvokeAsync(args);
