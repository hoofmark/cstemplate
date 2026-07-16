# cstemplate Example 1: Hello World

The basic `Hello World` template, with no additional configuration.

## Template Source Code

sample01.template.cs
```csharp
using HoofMark.CSharpTemplating.Abstractions;

namespace samples.sample01;

/// <summary>
/// A basic 'Hello World' template that writes a single text file to the default output directory.
/// </summary>
public class Template01 : ITemplate
{
    public static void Run(ITemplateContext context)
    {
        context.WriteFile("sample01.txt", "Hello from Template01!");
    }
}
```

## Output:

```bash
[4:51:21 PM] Running template: c:\Users\hoofmark\source\repos\vscode-extensions\template-test\samples\sample01\sample01.template.cs

✓ Template 'Template01' completed successfully.
  Output root: c:\Users\hoofmark\source\repos\vscode-extensions\template-test\samples\sample01\generated
  Generated 1 file(s):
    → c:\Users\hoofmark\source\repos\vscode-extensions\template-test\samples\sample01\generated\sample01.txt
```

No output directory was configured for this template, so the output is routed to a default `generated` folder which is created alongside the template.cs file.

Contents of sample01.txt:

```bash
Hello from Template01!
```