using FluentAssertions;
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

        w.ToString().Should().Be(Environment.NewLine);
    }

    [Fact]
    public void WriteLine_WithText_WritesTextAndNewline()
    {
        var w = new OutputWriter();
        w.WriteLine("hello");

        w.ToString().Should().Be($"hello{Environment.NewLine}");
    }

    [Fact]
    public void WriteLine_MultipleLines_AllWritten()
    {
        var w = new OutputWriter();
        w.WriteLine("a").WriteLine("b").WriteLine("c");

        var lines = w.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines.Should().BeEquivalentTo(["a", "b", "c"]);
    }

    // ── Write ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Write_DoesNotAppendNewline()
    {
        var w = new OutputWriter();
        w.Write("hello");

        w.ToString().Should().Be("hello");
    }

    [Fact]
    public void Write_ThenWriteLine_CombinesOnSameLine()
    {
        var w = new OutputWriter();
        w.Write("hello ").WriteLine("world");

        w.ToString().Should().Be($"hello world{Environment.NewLine}");
    }

    [Fact]
    public void Write_EmptyString_WritesNothing()
    {
        var w = new OutputWriter();
        w.Write("");

        w.ToString().Should().BeEmpty();
    }

    // ── Indentation ───────────────────────────────────────────────────────────

    [Fact]
    public void Indent_IncreasesIndentLevel()
    {
        var w = new OutputWriter();
        w.Indent();

        w.IndentLevel.Should().Be(1);
    }

    [Fact]
    public void Dedent_DecreasesIndentLevel()
    {
        var w = new OutputWriter();
        w.Indent().Indent().Dedent();

        w.IndentLevel.Should().Be(1);
    }

    [Fact]
    public void Dedent_BelowZero_ClampsToZero()
    {
        var w = new OutputWriter();
        w.Dedent(5);

        w.IndentLevel.Should().Be(0);
    }

    [Fact]
    public void WriteLine_AfterIndent_PrependsFourSpaces()
    {
        var w = new OutputWriter();
        w.Indent().WriteLine("indented");

        w.ToString().Should().Be($"    indented{Environment.NewLine}");
    }

    [Fact]
    public void WriteLine_AfterDoubleIndent_PrependsEightSpaces()
    {
        var w = new OutputWriter();
        w.Indent(2).WriteLine("deep");

        w.ToString().Should().Be($"        deep{Environment.NewLine}");
    }

    [Fact]
    public void Indent_ThenDedent_RestoredToOriginalLevel()
    {
        var w = new OutputWriter();
        w.WriteLine("outer");
        w.Indent().WriteLine("inner").Dedent();
        w.WriteLine("outer-again");

        var lines = w.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        lines[0].Should().Be("outer");
        lines[1].Should().Be("    inner");
        lines[2].Should().Be("outer-again");
    }

    [Fact]
    public void Write_AtIndentedLevel_PrependIndentOnFirstWriteOfLine()
    {
        var w = new OutputWriter();
        w.Indent();
        w.Write("a").Write("b").WriteLine("c");

        // Indent should only be prepended once at the start of the line
        w.ToString().Should().Be($"    abc{Environment.NewLine}");
    }

    // ── Custom indent unit ────────────────────────────────────────────────────

    [Fact]
    public void CustomIndentUnit_UsedInsteadOfDefaultFourSpaces()
    {
        var w = new OutputWriter(indentUnit: "\t");
        w.Indent().WriteLine("tabbed");

        w.ToString().Should().Be($"\ttabbed{Environment.NewLine}");
    }

    // ── Block() ───────────────────────────────────────────────────────────────

    [Fact]
    public void Block_WritesHeaderBracesAndBody()
    {
        var w = new OutputWriter();
        w.Block("public class Foo", body => body.WriteLine("int x;"));

        var result = w.ToString();
        result.Should().Contain("public class Foo");
        result.Should().Contain("{");
        result.Should().Contain("    int x;");
        result.Should().Contain("}");
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

        lines.Should().Contain("namespace Foo");
        lines.Should().Contain("{");
        lines.Should().Contain("    public class Bar");
        lines.Should().Contain("    {");
        lines.Should().Contain("        int x;");
        lines.Should().Contain("    }");
        lines.Should().Contain("}");
    }

    [Fact]
    public void Block_TrailingSemicolon_AppendsToClosingBrace()
    {
        var w = new OutputWriter();
        w.Block("struct Foo", body => body.WriteLine("int x;"), trailingSemicolon: true);

        w.ToString().Should().Contain("};");
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

        lines.Last().Should().Be("after");
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

        returned.Should().BeSameAs(w);
    }
}
