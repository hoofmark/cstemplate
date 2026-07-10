using System.CommandLine;
using System.CommandLine.Parsing;
using HoofMark.CSharpTemplating.Cli;

// Root command: cstemplate
var root = new RootCommand("cstemplate — TemplateEngine CLI");

root.Subcommands.Add(RunCommandFactory.Build());
root.Subcommands.Add(CheckCommandFactory.Build());

var parseResult = CommandLineParser.Parse(root, args, new ParserConfiguration());
return await parseResult.InvokeAsync();
