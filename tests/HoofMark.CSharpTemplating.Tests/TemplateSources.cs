namespace HoofMark.CSharpTemplating.Tests.Helpers;

/// <summary>
/// Reusable template source strings for use across tests.
/// All sources are valid C# that compile against HoofMark.CSharpTemplating.Abstractions.
/// </summary>
internal static class TemplateSources
{
    /// <summary>Writes a single file with fixed content.</summary>
    public const string SingleFile = """
        using HoofMark.CSharpTemplating.Abstractions;
        public class T : ITemplate {
            public static void Run(ITemplateContext ctx) =>
                ctx.WriteFile("output.txt", "hello from template");
        }
        """;

    /// <summary>Writes two files to different relative paths.</summary>
    public const string MultipleFiles = """
        using HoofMark.CSharpTemplating.Abstractions;
        public class T : ITemplate {
            public static void Run(ITemplateContext ctx) {
                ctx.WriteFile("a.txt", "file-a");
                ctx.WriteFile("sub/b.txt", "file-b");
            }
        }
        """;

    /// <summary>Reads a config value and writes it to the output file.</summary>
    public const string UsesConfig = """
        using HoofMark.CSharpTemplating.Abstractions;
        public class T : ITemplate {
            public static void Run(ITemplateContext ctx) =>
                ctx.WriteFile("output.txt", ctx.Config.Get("key") ?? "missing");
        }
        """;

    /// <summary>Uses IOutputWriter to build indented content.</summary>
    public const string UsesOutputWriter = """
        using HoofMark.CSharpTemplating.Abstractions;
        public class T : ITemplate {
            public static void Run(ITemplateContext ctx) =>
                ctx.WriteFile("output.txt", w => w
                    .WriteLine("line1")
                    .Indent()
                        .WriteLine("line2")
                    .Dedent()
                    .WriteLine("line3"));
        }
        """;

    /// <summary>Uses IOutputWriter.Block() to write a C#-style class.</summary>
    public const string UsesOutputWriterBlock = """
        using HoofMark.CSharpTemplating.Abstractions;
        public class T : ITemplate {
            public static void Run(ITemplateContext ctx) =>
                ctx.WriteFile("Class.cs", w => w
                    .Block("public class Foo", body => body
                        .WriteLine("public int Id { get; set; }")
                    ));
        }
        """;

    /// <summary>Throws an exception during Run().</summary>
    public const string ThrowsDuringRun = """
        using HoofMark.CSharpTemplating.Abstractions;
        public class T : ITemplate {
            public static void Run(ITemplateContext ctx) =>
                throw new System.InvalidOperationException("template-error");
        }
        """;

    /// <summary>Attempts path traversal via WriteFile.</summary>
    public const string PathTraversal = """
        using HoofMark.CSharpTemplating.Abstractions;
        public class T : ITemplate {
            public static void Run(ITemplateContext ctx) =>
                ctx.WriteFile("../../etc/passwd", "pwned");
        }
        """;

    /// <summary>Contains a compile error (undefined variable).</summary>
    public const string CompileError = """
        using HoofMark.CSharpTemplating.Abstractions;
        public class T : ITemplate {
            public static void Run(ITemplateContext ctx) {
                var x = undefinedVariable;
                ctx.WriteFile("output.txt", x);
            }
        }
        """;

    /// <summary>Does not implement ITemplate at all.</summary>
    public const string NoITemplate = """
        public class T {
            public static void Run() { }
        }
        """;

    /// <summary>Has a syntax error.</summary>
    public const string SyntaxError = """
        using HoofMark.CSharpTemplating.Abstractions;
        public class T : ITemplate {
            public static void Run(ITemplateContext ctx) {
                ctx.WriteFile("output.txt" "missing comma");
            }
        }
        """;

    /// <summary>
    /// Reads a sibling file named "model.txt" and writes its content to output.txt.
    /// </summary>
    public const string ReadsFile = """
        using HoofMark.CSharpTemplating.Abstractions;
        public class T : ITemplate {
            public static void Run(ITemplateContext ctx) =>
                ctx.WriteFile("output.txt", ctx.ReadFile("model.txt"));
        }
        """;

    /// <summary>Two classes both implementing ITemplate.</summary>
    public const string MultipleITemplates = """
        using HoofMark.CSharpTemplating.Abstractions;
        public class T1 : ITemplate {
            public static void Run(ITemplateContext ctx) { }
        }
        public class T2 : ITemplate {
            public static void Run(ITemplateContext ctx) { }
        }
        """;

    /// <summary>
    /// Reads a strongly-typed list from config and writes one file per item.
    /// Config JSON: { "Items": ["a", "b", "c"] }
    /// </summary>
    public const string UsesTypedConfig = """
        using HoofMark.CSharpTemplating.Abstractions;
        using System.Collections.Generic;
        public class T : ITemplate {
            public static void Run(ITemplateContext ctx) {
                var items = ctx.Config.Get<List<string>>("items") ?? new();
                foreach (var item in items)
                    ctx.WriteFile($"{item}.txt", item);
            }
        }
        """;
}
