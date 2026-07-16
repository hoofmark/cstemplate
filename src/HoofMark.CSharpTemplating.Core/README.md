# HoofMark.CSharpTemplating.Core

Generate code or any other text output from template files written in pure C#. 

`cstemplate` is a VSCode extension and CLI tool that runs C# template files (`.template.cs`) directly from VS Code.

`HoofMark.CSharpTemplating.Core` provides the core functionality for compiling and running templates.

## Getting started

- Install the CLI tool `cstemplate` via tool package `HoofMark.CSharpTemplating.Cli`
- Install the `cstemplate` extension from the [VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=HoofMark.cstemplate-vscode)
- Add a package reference to `HoofMark.CSharpTemplating.Abstractions` in your template .csproj file
- Create a template (`*.template.cs`) and optional config (`*.json`) file in the project
- Using VSCode context menu, shortcut keys or command palette, run the template to generate output.

For full documentation, see [hoofmark.github.io/cstemplate](https://hoofmark.github.io/cstemplate).

### Prerequisites

- .NET 10 SDK or later
- VS Code with the [C# Dev Kit](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) extension

## Usage

The `cstemplate` extension for VSCode provides functionality that is similar to T4 templates in Visual Studio, but without the need to learn a separate templating language or to edit complex syntax containing a mix of C# code and embedded template instructions. Template files (`*.template.cs`) are written in pure C#, so can use any C# language features (StringBuilder, interpolated strings, etc) to generate and format output. If required, templates can also reference NuGet packages and custom DLLs at design time. Standard C# Dev Kit functionality and intellisense provides full support for editing, compiling and debugging templates, while the extension provides context menu, keyboard shortcuts and command palette options to generate code directly within VSCode. 

Configuration files placed alongside the templates can be used to define template inputs (argument values, `.json` or `.xml` models, database connections, etc). The same template can be reused in multiple projects to generate output specific to each project.

Template output is simply one or more text files, written to a target location relative to the current project / solution. An individual template can be used to generate a single C# class, produce boilerplate code for data access objects, or to scaffold an entire project.

## Additional documentation

For full documentation, see [hoofmark.github.io/cstemplate](https://hoofmark.github.io/cstemplate).

## Feedback

For source code and to log issues and feedback, see [GitHub](https://github.com/hoofmark/cstemplate).

