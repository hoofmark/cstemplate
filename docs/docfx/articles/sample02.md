# cstemplate Example 2: Hello World with Configuration

The basic `Hello World` template, extended with output path and variable substitution using context and template configuration files.

## Template Source Code: 

sample02.template.cs
```csharp
using HoofMark.CSharpTemplating.Abstractions;

namespace samples.sample02;

/// <summary>
/// A basic 'Hello World' template that writes a single text file to the output directory.
/// The output file root directory is specified in the template configuration file (cstemplate.config.json).
/// The output file relative path and message text are read from the template configuration file (sample02.template.json).
/// </summary>
public class Template02 : ITemplate
{
    public static void Run(ITemplateContext context)
    {
        var outputFileName = context.Config.Get<string>("outputFileName") ?? "undefined.txt";
        var messageText = context.Config.Get<string>("messageText");
        context.WriteFile(outputFileName, $"Hello from {messageText}!");
    }
}
```

## Template config

This shows how to define values for named variables, which can be retrieved from configuration by the template. Note that variable names are case-sensitive.

Note that `outputFileName` in this case contains a relative path, this is resolved relative to the `outputRoot` path defined in the template context (or relative to the default `generated` subfolder, if no context is defined).

sample02.template.json
```json
{
  "messageText": "configuration for sample02 template",
  "outputFileName": "docs/sample02.txt"
}
```

## Template context

For simplicity in these examples, the template context file is placed in the same directory as the template itself. In general, the context file can be placed anywhere in the folder hierarchy; `cstemplate` will scan parent folders and will use the first context file it finds in the folder tree.

cstemplate.config.json
```json
{
  "outputRoot": "../docs/sample02",
  "project":    "../samples.csproj",
  "nugetPackages": [],
  "references": []
}
```

- `output root` identifies the path to the root folder, relative to the location of cstemplate.config.json
- `project` is the parent *.csproj file that "owns" the *.template.cs file
- `nugetPackages` lists referenced NuGet packages; not used in this example
- `references` lists local assembly references; not used in this example

## Output:

```bash
[5:42:34 PM] Running template: c:\Users\hoofmark\source\repos\vscode-extensions\template-test\samples\sample02\sample02.template.cs

✓ Template 'Template02' completed successfully.
  Output root: c:\Users\hoofmark\source\repos\vscode-extensions\template-test\samples\docs\sample02
  Generated 1 file(s):
    → c:\Users\hoofmark\source\repos\vscode-extensions\template-test\samples\docs\sample02\sample02.txt
```

The output file path is relative to the outputRoot path specified in `cstemplate.config.json`. 

Substitution values for output file name and message text are retrieved from `sample02.template.json`

Contents of sample02.txt:

```bash
Hello from configuration for sample02 template!
```