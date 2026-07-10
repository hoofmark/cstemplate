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

        result.FileCount.ShouldBe(1);
        (await dir.ReadAsync("output.txt")).ShouldBe("hello from template");
    }

    [Fact]
    public async Task RunAsync_MultipleFiles_AllWritten()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.MultipleFiles);

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.FileCount.ShouldBe(2);
        (await dir.ReadAsync("a.txt")).ShouldBe("file-a");
        (await dir.ReadAsync("sub/b.txt")).ShouldBe("file-b");
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectTemplateName()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.TemplateName.ShouldBe("T");
    }

    [Fact]
    public async Task RunAsync_GeneratedFilesListContainsAbsolutePaths()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        // result.GeneratedFiles.ShouldAllSatisfy(p => Path.IsPathRooted(p).ShouldBeTrue());
        result.GeneratedFiles.ToList().ForEach(p => Path.IsPathRooted(p).ShouldBeTrue());
        result.GeneratedFiles.ShouldAllBe(p => Path.IsPathRooted(p));
    }

    // ── Config loading ────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_WithSiblingConfig_ConfigAvailableInTemplate()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs",  TemplateSources.UsesConfig);
        await dir.WriteAsync("T.json", """{"key":"from-config"}""");

        await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        (await dir.ReadAsync("output.txt")).ShouldBe("from-config");
    }

    [Fact]
    public async Task RunAsync_NoConfigFile_ConfigGetReturnsMissing()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.UsesConfig);
        // No T.json sibling

        await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        (await dir.ReadAsync("output.txt")).ShouldBe("missing");
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

        (await dir.ReadAsync("output.txt")).ShouldBe("overridden");
    }

    [Fact]
    public async Task RunAsync_TypedConfig_DeserialisedCorrectly()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs",  TemplateSources.UsesTypedConfig);
        await dir.WriteAsync("T.json", """{"items":["x","y","z"]}""");

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.FileCount.ShouldBe(3);
        dir.Exists("x.txt").ShouldBeTrue();
        dir.Exists("y.txt").ShouldBeTrue();
        dir.Exists("z.txt").ShouldBeTrue();
    }

    // ── Output root ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_NoOutputRoot_DefaultsToGeneratedSubfolder()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        var result = await RunAsync(dir.File("T.template.cs"), cancellationToken: TestContext.Current.CancellationToken);

        result.OutputRoot.ShouldEndWith("generated");
        File.Exists(Path.Combine(result.OutputRoot, "output.txt")).ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_WithOutputRoot_WritesToSpecifiedDirectory()
    {
        using var dir = new TempDirectory();
        using var outDir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: outDir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.OutputRoot.ShouldBe(outDir.Path);
        File.Exists(Path.Combine(outDir.Path, "output.txt")).ShouldBeTrue();
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

        lines[0].ShouldBe("line1");
        lines[1].ShouldBe("    line2");   // indented
        lines[2].ShouldBe("line3");       // dedented back
    }

    [Fact]
    public async Task RunAsync_UsesOutputWriterBlock_GeneratesValidCShapeStructure()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.UsesOutputWriterBlock);

        await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        var content = await dir.ReadAsync("Class.cs");
        content.ShouldContain("public class Foo");
        content.ShouldContain("{");
        content.ShouldContain("    public int Id { get; set; }");
        content.ShouldContain("}");
    }

    // ── Error cases ───────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_CompileError_ThrowsTemplateCompilationException()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.CompileError);

        var act = () => _runner.RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path);

        await act.ShouldThrowAsync<TemplateCompilationException>();
    }

    [Fact]
    public async Task RunAsync_RuntimeException_ThrowsTemplateExecutionException()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.ThrowsDuringRun);

        var act = () => _runner.RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path);

        var ex = await Assert.ThrowsAsync<TemplateExecutionException>(act);
        ex.InnerException.ShouldBeOfType<InvalidOperationException>();
        ex.InnerException!.Message.ShouldContain("template-error");
        ex.TemplateName.ShouldBe("T");
    }

    [Fact]
    public async Task RunAsync_PathTraversal_ThrowsTemplateExecutionException()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.PathTraversal);

        var act = () => _runner.RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path);

        await act.ShouldThrowAsync<TemplateExecutionException>();
    }

    [Fact]
    public async Task RunAsync_MissingTemplateFile_ThrowsFileNotFoundException()
    {
        var act = () => _runner.RunAsync("/nonexistent/T.cst");

        await act.ShouldThrowAsync<FileNotFoundException>();
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

        await act.ShouldThrowAsync<OperationCanceledException>();
    }

    // ── ReadFile ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task RunAsync_ReadFile_FindsSiblingFile()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.ReadsFile);
        await dir.WriteAsync("model.txt", "model-content");

        await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        (await dir.ReadAsync("output.txt")).ShouldBe("model-content");
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

        (await outputDir.ReadAsync("output.txt")).ShouldBe("from-project-root");
    }

    [Fact]
    public async Task RunAsync_ReadFile_NotFound_ThrowsTemplateExecutionException()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.ReadsFile);
        // No model.txt

        var act = () => RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path);

        var ex = await act.ShouldThrowAsync<TemplateExecutionException>();
        ex.InnerException?.GetType().ShouldBe(typeof(FileNotFoundException));
            // .WithInnerException<TemplateExecutionException, FileNotFoundException>();
    }

    // ── ToString / display ────────────────────────────────────────────────────

    [Fact]
    public async Task RunResult_ToString_IncludesTemplateNameAndFileCount()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("T.template.cs", TemplateSources.SingleFile);

        var result = await RunAsync(dir.File("T.template.cs"), outputRoot: dir.Path, cancellationToken: TestContext.Current.CancellationToken);

        result.ToString().ShouldContain("T");
        result.ToString().ShouldContain("1");
    }
}
