using FluentAssertions;
using HoofMark.CSharpTemplating.Core;
using HoofMark.CSharpTemplating.Tests.Helpers;

namespace HoofMark.CSharpTemplating.Tests;

public class TemplateCompilerTests
{
    private readonly TemplateCompiler _compiler = new();
    private readonly ITestOutputHelper _output;

    public TemplateCompilerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private CompiledTemplate CompileSource(string source)
    {
        try
        {
            return _compiler.CompileSource(source);
        }
        catch (TemplateCompilationException ex)
        {
            _output.WriteLine($"Compilation failed:\n{ex}");
            throw;
        }
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public void Compile_ValidSource_ReturnsCompiledTemplate()
    {
        using var compiled = CompileSource(TemplateSources.SingleFile);

        compiled.Should().NotBeNull();
        compiled.TemplateName.Should().Be("T");
    }

    [Fact]
    public void Compile_ValidFile_ReturnsCompiledTemplate()
    {
        using var dir = new TempDirectory();
        var path = dir.File("T.template.cs");
        File.WriteAllText(path, TemplateSources.SingleFile);

        using var compiled = _compiler.Compile(path);

        compiled.TemplateName.Should().Be("T");
    }

    [Fact]
    public void Compile_DisposedTemplate_UnloadsAssembly()
    {
        var compiled = CompileSource(TemplateSources.SingleFile);
        compiled.Dispose();

        // Calling Run after disposal should throw ObjectDisposedException
        var act = () => compiled.Run(null!);
        act.Should().Throw<ObjectDisposedException>();
    }

    // ── Compile errors ────────────────────────────────────────────────────────

    [Fact]
    public void Compile_CompileError_ThrowsWithDiagnostics()
    {
        var act = () => _compiler.CompileSource(TemplateSources.CompileError);

        act.Should().Throw<TemplateCompilationException>()
            .Which.Diagnostics.Should().NotBeEmpty();
    }

    [Fact]
    public void Compile_CompileError_DiagnosticsHaveLineNumbers()
    {
        var ex = Assert.Throws<TemplateCompilationException>(
            () => _compiler.CompileSource(TemplateSources.CompileError));

        ex.Diagnostics.Should().AllSatisfy(d =>
        {
            d.Line.Should().BeGreaterThan(0);
            d.Column.Should().BeGreaterThan(0);
            d.Message.Should().NotBeNullOrWhiteSpace();
        });
    }

    [Fact]
    public void Compile_SyntaxError_ThrowsCompilationException()
    {
        var act = () => _compiler.CompileSource(TemplateSources.SyntaxError);

        act.Should().Throw<TemplateCompilationException>();
    }

    // ── ITemplate discovery ───────────────────────────────────────────────────

    [Fact]
    public void Compile_NoITemplate_ThrowsWithHelpfulMessage()
    {
        var act = () => _compiler.CompileSource(TemplateSources.NoITemplate);

        act.Should().Throw<TemplateCompilationException>()
            .WithMessage("*ITemplate*");
    }

    [Fact]
    public void Compile_MultipleITemplates_ThrowsWithBothNames()
    {
        var act = () => _compiler.CompileSource(TemplateSources.MultipleITemplates);

        act.Should().Throw<TemplateCompilationException>()
            .WithMessage("*T1*")
            .And.Message.Should().Contain("T2");
    }

    // ── File not found ────────────────────────────────────────────────────────

    [Fact]
    public void Compile_MissingFile_ThrowsFileNotFoundException()
    {
        var act = () => _compiler.Compile("/nonexistent/path/T.cst");

        act.Should().Throw<FileNotFoundException>();
    }

    // ── Additional references ─────────────────────────────────────────────────

    [Fact]
    public void Compile_WithAdditionalReference_CanUseTypesFromIt()
    {
        // The test assembly itself is a valid additional reference
        var testAssemblyPath = typeof(TemplateCompilerTests).Assembly.Location;
        var compiler = new TemplateCompiler(additionalReferencePaths: [testAssemblyPath]);

        // Should compile without error — we're just checking no exception is thrown
        var act = () => compiler.CompileSource(TemplateSources.SingleFile);
        act.Should().NotThrow();
    }

    [Fact]
    public void Compile_WithMissingAdditionalReference_ThrowsFileNotFoundException()
    {
        var compiler = new TemplateCompiler(additionalReferencePaths: ["/nonexistent/lib.dll"]);

        var act = () => compiler.CompileSource(TemplateSources.SingleFile);
        act.Should().Throw<FileNotFoundException>();
    }
}
