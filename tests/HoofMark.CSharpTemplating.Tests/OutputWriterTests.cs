using HoofMark.CSharpTemplating.Core;

namespace HoofMark.CSharpTemplating.Tests;

public class OutputWriterTests
{
    // ── WriteLine ─────────────────────────────────────────────────────────────

    [Fact]
    public void WriteLine_NoArgs_WritesBlankLine()
    {
        var w = new OutputWriter();
        w.WriteLine();

        w.ToString().ShouldBe(Environment.NewLine);
    }

    [Fact]
    public void WriteLine_WithText_WritesTextAndNewline()
    {
        var w = new OutputWriter();
        w.WriteLine("hello");

        w.ToString().ShouldBe($"hello{Environment.NewLine}");
    }

    [Fact]
    public void WriteLine_MultipleLines_AllWritten()
    {
        var w = new OutputWriter();
        w.WriteLine("a").WriteLine("b").WriteLine("c");

        var lines = w.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.ShouldBeEquivalentTo(new [] {"a", "b", "c"});
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Write_DoesNotAppendNewline()
    {
        var w = new OutputWriter();
        w.Write("hello");

        w.ToString().ShouldBe("hello");
    }

    [Fact]
    public void Write_ThenWriteLine_CombinesOnSameLine()
    {
        var w = new OutputWriter();
        w.Write("hello ").WriteLine("world");

        w.ToString().ShouldBe($"hello world{Environment.NewLine}");
    }

    [Fact]
    public void Write_EmptyString_WritesNothing()
    {
        var w = new OutputWriter();
        w.Write("");

        w.ToString().ShouldBeEmpty();
    }

    // ── Indentation ───────────────────────────────────────────────────────────

    [Fact]
    public void Indent_IncreasesIndentLevel()
    {
        var w = new OutputWriter();
        w.Indent();

        w.IndentLevel.ShouldBe(1);
    }

    [Fact]
    public void Dedent_DecreasesIndentLevel()
    {
        var w = new OutputWriter();
        w.Indent().Indent().Dedent();

        w.IndentLevel.ShouldBe(1);
    }

    [Fact]
    public void Dedent_BelowZero_ClampsToZero()
    {
        var w = new OutputWriter();
        w.Dedent(5);

        w.IndentLevel.ShouldBe(0);
    }

    [Fact]
    public void WriteLine_AfterIndent_PrependsFourSpaces()
    {
        var w = new OutputWriter();
        w.Indent().WriteLine("indented");

        w.ToString().ShouldBe($"    indented{Environment.NewLine}");
    }

    [Fact]
    public void WriteLine_AfterDoubleIndent_PrependsEightSpaces()
    {
        var w = new OutputWriter();
        w.Indent(2).WriteLine("deep");

        w.ToString().ShouldBe($"        deep{Environment.NewLine}");
    }

    [Fact]
    public void Indent_ThenDedent_RestoredToOriginalLevel()
    {
        var w = new OutputWriter();
        w.WriteLine("outer");
        w.Indent().WriteLine("inner").Dedent();
        w.WriteLine("outer-again");

        var lines = w.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[0].ShouldBe("outer");
        lines[1].ShouldBe("    inner");
        lines[2].ShouldBe("outer-again");
    }

    [Fact]
    public void Write_AtIndentedLevel_PrependIndentOnFirstWriteOfLine()
    {
        var w = new OutputWriter();
        w.Indent();
        w.Write("a").Write("b").WriteLine("c");

        // Indent should only be prepended once at the start of the line
        w.ToString().ShouldBe($"    abc{Environment.NewLine}");
    }

    // ── Custom indent unit ────────────────────────────────────────────────────

    [Fact]
    public void CustomIndentUnit_UsedInsteadOfDefaultFourSpaces()
    {
        var w = new OutputWriter(indentUnit: "\t");
        w.Indent().WriteLine("tabbed");

        w.ToString().ShouldBe($"\ttabbed{Environment.NewLine}");
    }

    // ── Block() ───────────────────────────────────────────────────────────────

    [Fact]
    public void Block_WritesHeaderBracesAndBody()
    {
        var w = new OutputWriter();
        w.Block("public class Foo", body => body.WriteLine("int x;"));

        var result = w.ToString();
        result.ShouldContain("public class Foo");
        result.ShouldContain("{");
        result.ShouldContain("    int x;");
        result.ShouldContain("}");
    }

    [Fact]
    public void Block_BodyIsIndented()
    {
        var w = new OutputWriter();
        w.Block("namespace Foo", body => body
            .Block("public class Bar", inner => inner
                .WriteLine("int x;")));

        var lines = w.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        lines.ShouldContain("namespace Foo");
        lines.ShouldContain("{");
        lines.ShouldContain("    public class Bar");
        lines.ShouldContain("    {");
        lines.ShouldContain("        int x;");
        lines.ShouldContain("    }");
        lines.ShouldContain("}");
    }

    [Fact]
    public void Block_TrailingSemicolon_AppendsToClosingBrace()
    {
        var w = new OutputWriter();
        w.Block("struct Foo", body => body.WriteLine("int x;"), trailingSemicolon: true);

        w.ToString().ShouldContain("};");
    }

    [Fact]
    public void Block_AfterBlock_IndentLevelRestored()
    {
        var w = new OutputWriter();
        w.Block("class Foo", _ => { });
        w.WriteLine("after");

        // "after" should be at level 0 — no leading spaces
        var lines = w.ToString()
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        lines.Last().ShouldBe("after");
    }

    // ── Fluent chaining ───────────────────────────────────────────────────────

    [Fact]
    public void AllMethods_ReturnSameInstance_ForChaining()
    {
        var w = new OutputWriter();

        var returned = w
            .Write("a")
            .WriteLine("b")
            .Indent()
            .Dedent()
            .Block("x", _ => { });

        returned.ShouldBeSameAs(w);
    }
}
