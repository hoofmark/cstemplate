using FluentAssertions;
using HoofMark.CSharpTemplating.Core;
using HoofMark.CSharpTemplating.Tests.Helpers;

namespace HoofMark.CSharpTemplating.Tests;

/// <summary>
/// End-to-end tests for <see cref="TemplateRunner"/>.
/// These exercise the full pipeline: config loading → Roslyn compile →
/// context execution → file flush. They are slower than unit tests
/// (each involves a real Roslyn compilation) but are the most faithful
/// representation of real usage.
/// </summary>
[Trait("Category", "Integration")]
public class TemplateRunnerTests
{
    private readonly TemplateRunner _runner = new();
    private readonly ITestOutputHelper _output;

    public TemplateRunnerTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private async Task<TemplateRunResult> RunAsync(
        string templatePath,
        string? outputRoot = null,
        string? configOverridePath = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _runner.RunAsync(templatePath, outputRoot, configOverridePath, cancellationToken);
        }
        catch (TemplateCompilationException ex)
        {
            _output.WriteLine($"Compilation failed:\n{ex}");
            throw;
        }
    }

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_SingleFile_WritesOutputFile()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.FileCount.Should().Be(1);
        (await dir.ReadAsync("output.txt")).Should().Be("hello from template");
    }

    [Fact]
    public async Task RunAsync_MultipleFiles_AllWritten()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.MultipleFiles);

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.FileCount.Should().Be(2);
        (await dir.ReadAsync("a.txt")).Should().Be("file-a");
        (await dir.ReadAsync("sub/b.txt")).Should().Be("file-b");
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectTemplateName()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.TemplateName.Should().Be("T");
    }

    [Fact]
    public async Task RunAsync_GeneratedFilesListContainsAbsolutePaths()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.GeneratedFiles.Should().AllSatisfy(p => Path.IsPathRooted(p).Should().BeTrue());
    }

    // ── Config loading ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WithSiblingConfig_ConfigAvailableInTemplate()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs",  TemplateSources.UsesConfig);
        await dir.WriteAsync("T.json", """{"key":"from-config"}""");

        await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        (await dir.ReadAsync("output.txt")).Should().Be("from-config");
    }

    [Fact]
    public async Task RunAsync_NoConfigFile_ConfigGetReturnsMissing()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.UsesConfig);
        // No T.json sibling

        await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        (await dir.ReadAsync("output.txt")).Should().Be("missing");
    }

    [Fact]
    public async Task RunAsync_WithConfigOverride_UsesOverrideFile()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs",           TemplateSources.UsesConfig);
        await dir.WriteAsync("T.json",           """{"key":"default"}""");
        await dir.WriteAsync("override.json",    """{"key":"overridden"}""");

        await RunAsync(
            dir.File("T.template.cs"),
            outputRoot: dir.Path,
            configOverridePath: dir.File("override.json"),
            cancellationToken: TestContext.Current.CancellationToken);

        (await dir.ReadAsync("output.txt")).Should().Be("overridden");
    }

    [Fact]
    public async Task RunAsync_TypedConfig_DeserialisedCorrectly()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs",  TemplateSources.UsesTypedConfig);
        await dir.WriteAsync("T.json", """{"items":["x","y","z"]}""");

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.FileCount.Should().Be(3);
        dir.Exists("x.txt").Should().BeTrue();
        dir.Exists("y.txt").Should().BeTrue();
        dir.Exists("z.txt").Should().BeTrue();
    }

    // ── Output root ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_NoOutputRoot_DefaultsToGeneratedSubfolder()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        var result = await RunAsync(dir.File("T.template.cs"), cancellationToken: TestContext.Current.CancellationToken);

        result.OutputRoot.Should().EndWith("generated");
        File.Exists(Path.Combine(result.OutputRoot, "output.txt")).Should().BeTrue();
    }

    [Fact]
    public async Task RunAsync_WithOutputRoot_WritesToSpecifiedDirectory()
    {
        using var dir = new TempDirectory();
        using var outDir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: outDir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.OutputRoot.Should().Be(outDir.Path);
        File.Exists(Path.Combine(outDir.Path, "output.txt")).Should().BeTrue();
    }

    // ── IOutputWriter ─────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_UsesOutputWriter_IndentationApplied()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.UsesOutputWriter);

        await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        var content = await dir.ReadAsync("output.txt");
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

        lines[0].Should().Be("line1");
        lines[1].Should().Be("    line2");   // indented
        lines[2].Should().Be("line3");       // dedented back
    }

    [Fact]
    public async Task RunAsync_UsesOutputWriterBlock_GeneratesValidCShapeStructure()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.UsesOutputWriterBlock);

        await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        var content = await dir.ReadAsync("Class.cs");
        content.Should().Contain("public class Foo");
        content.Should().Contain("{");
        content.Should().Contain("    public int Id { get; set; }");
        content.Should().Contain("}");
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CompileError_ThrowsTemplateCompilationException()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.CompileError);

        var act = () => _runner.RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path);

        await act.Should().ThrowAsync<TemplateCompilationException>();
    }

    [Fact]
    public async Task RunAsync_RuntimeException_ThrowsTemplateExecutionException()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.ThrowsDuringRun);

        var act = () => _runner.RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path);

        var ex = await Assert.ThrowsAsync<TemplateExecutionException>(act);
        ex.InnerException.Should().BeOfType<InvalidOperationException>();
        ex.InnerException!.Message.Should().Contain("template-error");
        ex.TemplateName.Should().Be("T");
    }

    [Fact]
    public async Task RunAsync_PathTraversal_ThrowsTemplateExecutionException()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.PathTraversal);

        var act = () => _runner.RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path);

        await act.Should().ThrowAsync<TemplateExecutionException>();
    }

    [Fact]
    public async Task RunAsync_MissingTemplateFile_ThrowsFileNotFoundException()
    {
        var act = () => _runner.RunAsync("/nonexistent/T.cst");

        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    // ── Cancellation ──────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CancelledToken_ThrowsOperationCancelledException()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => _runner.RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── ReadFile ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ReadFile_FindsSiblingFile()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.ReadsFile);
        await dir.WriteAsync("model.txt", "model-content");

        await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        (await dir.ReadAsync("output.txt")).Should().Be("model-content");
    }

    [Fact]
    public async Task RunAsync_ReadFile_FindsFileInProjectRoot()
    {
        using var templateDir = new TempDirectory();
        using var projectDir  = new TempDirectory();
        using var outputDir   = new TempDirectory();

        await templateDir.WriteAsync("T.template.cs", TemplateSources.ReadsFile);
        await projectDir.WriteAsync("model.txt", "from-project-root");

        var runner = new TemplateRunner(new TemplateRunnerOptions
        {
            ProjectRoot = projectDir.Path
        });

        await runner.RunAsync(templateDir.File("T.template.cs"), outputRoot: outputDir.Path, cancellationToken: TestContext.Current.CancellationToken);

        (await outputDir.ReadAsync("output.txt")).Should().Be("from-project-root");
    }

    [Fact]
    public async Task RunAsync_ReadFile_NotFound_ThrowsTemplateExecutionException()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.ReadsFile);
        // No model.txt

        var act = () => RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path);

        await act.Should().ThrowAsync<TemplateExecutionException>()
            .WithInnerException<TemplateExecutionException, FileNotFoundException>();
    }

    // ── ToString / display ────────────────────────────────────────────────────

    [Fact]
    public async Task RunResult_ToString_IncludesTemplateNameAndFileCount()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.ToString().Should().Contain("T");
        result.ToString().Should().Contain("1");
    }
}
