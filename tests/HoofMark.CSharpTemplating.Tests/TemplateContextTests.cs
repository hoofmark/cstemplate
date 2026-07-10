using FluentAssertions;
using HoofMark.CSharpTemplating.Core;
using HoofMark.CSharpTemplating.Tests.Helpers;

namespace HoofMark.CSharpTemplating.Tests;

public class TemplateContextTests
{
    private static TemplateContext MakeContext(string outputRoot, string? templateDir = null, string? projectRoot = null)
    {
        var dir = templateDir ?? outputRoot;
        return new TemplateContext(TemplateConfig.Empty, outputRoot, dir, projectRoot);
    }
	
    // ── WriteFile(path, string) ───────────────────────────────────────────────

    [Fact]
    public async Task WriteFile_String_CreatesFileWithContent()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        ctx.WriteFile("output.txt", "hello");
        await ctx.FlushAsync(cancellationToken: TestContext.Current.CancellationToken);

        (await dir.ReadAsync("output.txt")).Should().Be("hello");
    }

    [Fact]
    public async Task WriteFile_CreatesIntermediateDirectories()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        ctx.WriteFile("a/b/c/output.txt", "nested");
        await ctx.FlushAsync(cancellationToken: TestContext.Current.CancellationToken);

        (await dir.ReadAsync("a/b/c/output.txt")).Should().Be("nested");
    }

    [Fact]
    public async Task WriteFile_MultipleFiles_AllFlushed()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        ctx.WriteFile("a.txt", "aaa");
        ctx.WriteFile("b.txt", "bbb");
        ctx.WriteFile("sub/c.txt", "ccc");
        await ctx.FlushAsync(cancellationToken: TestContext.Current.CancellationToken);

        (await dir.ReadAsync("a.txt")).Should().Be("aaa");
        (await dir.ReadAsync("b.txt")).Should().Be("bbb");
        (await dir.ReadAsync("sub/c.txt")).Should().Be("ccc");
    }

    [Fact]
    public async Task WriteFile_ForwardSlashPath_NormalisedToCurrentPlatform()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        ctx.WriteFile("sub/output.txt", "content");
        await ctx.FlushAsync(cancellationToken: TestContext.Current.CancellationToken);

        dir.Exists("sub/output.txt").Should().BeTrue();
    }

    // ── WriteFile(path, Action<IOutputWriter>) ────────────────────────────────

    [Fact]
    public async Task WriteFile_Writer_ContentMatchesWriterOutput()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        ctx.WriteFile("output.txt", w => w
            .WriteLine("line1")
            .WriteLine("line2"));

        await ctx.FlushAsync(cancellationToken: TestContext.Current.CancellationToken);

        var content = await dir.ReadAsync("output.txt");
        content.Should().Contain("line1");
        content.Should().Contain("line2");
    }

    // ── FlushAsync return value ───────────────────────────────────────────────

    [Fact]
    public async Task FlushAsync_ReturnsAbsolutePathsOfWrittenFiles()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        ctx.WriteFile("a.txt", "a");
        ctx.WriteFile("b.txt", "b");

        var written = await ctx.FlushAsync(cancellationToken: TestContext.Current.CancellationToken);

        written.Should().HaveCount(2);
        written.Should().AllSatisfy(p => Path.IsPathRooted(p).Should().BeTrue());
        written.Should().Contain(p => p.EndsWith("a.txt"));
        written.Should().Contain(p => p.EndsWith("b.txt"));
    }

    [Fact]
    public async Task FlushAsync_NoFiles_ReturnsEmptyList()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        var written = await ctx.FlushAsync(cancellationToken: TestContext.Current.CancellationToken);

        written.Should().BeEmpty();
    }

    // ── Path security ─────────────────────────────────────────────────────────

    [Fact]
    public void WriteFile_PathTraversal_ThrowsTemplateExecutionException()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        var act = () => ctx.WriteFile("../../etc/passwd", "pwned");

        act.Should().Throw<TemplateExecutionException>()
            .WithMessage("*'..'*");
    }

    [Fact]
    public void WriteFile_AbsolutePath_ThrowsTemplateExecutionException()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        var act = () => ctx.WriteFile("/etc/passwd", "pwned");

        act.Should().Throw<TemplateExecutionException>()
            .WithMessage("*absolute*");
    }

    [Fact]
    public void WriteFile_NullPath_ThrowsArgumentException()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        var act = () => ctx.WriteFile(null!, "content");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void WriteFile_EmptyPath_ThrowsArgumentException()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        var act = () => ctx.WriteFile("   ", "content");

        act.Should().Throw<ArgumentException>();
    }

    // ── ReadFile ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ReadFile_AbsolutePath_ReturnsContent()
    {
        using var dir = new TempDirectory();
        await dir.WriteAsync("model.xml", "<root/>");
        var ctx = MakeContext(dir.Path);

        var content = ctx.ReadFile(dir.File("model.xml"));

        content.Should().Be("<root/>");
    }

    [Fact]
    public async Task ReadFile_RelativePath_FoundInTemplateDirectory()
    {
        using var templateDir = new TempDirectory();
        using var outputDir   = new TempDirectory();
        await templateDir.WriteAsync("model.xml", "<from-template-dir/>");

        var ctx = MakeContext(outputDir.Path, templateDir: templateDir.Path);

        var content = ctx.ReadFile("model.xml");

        content.Should().Be("<from-template-dir/>");
    }

    [Fact]
    public async Task ReadFile_RelativePath_FoundInProjectRoot()
    {
        using var templateDir = new TempDirectory();
        using var projectDir  = new TempDirectory();
        using var outputDir   = new TempDirectory();
        await projectDir.WriteAsync("shared/model.xml", "<from-project-root/>");

        var ctx = MakeContext(outputDir.Path, templateDir: templateDir.Path, projectRoot: projectDir.Path);

        var content = ctx.ReadFile("shared/model.xml");

        content.Should().Be("<from-project-root/>");
    }

    [Fact]
    public async Task ReadFile_RelativePath_FoundInOutputRoot()
    {
        using var templateDir = new TempDirectory();
        using var outputDir   = new TempDirectory();
        await outputDir.WriteAsync("generated.json", "{}");

        var ctx = MakeContext(outputDir.Path, templateDir: templateDir.Path);

        var content = ctx.ReadFile("generated.json");

        content.Should().Be("{}");
    }

    [Fact]
    public async Task ReadFile_RelativePath_TemplateDirTakesPriorityOverProjectRoot()
    {
        using var templateDir = new TempDirectory();
        using var projectDir  = new TempDirectory();
        using var outputDir   = new TempDirectory();

        // Same relative path exists in both — template dir should win
        await templateDir.WriteAsync("model.xml", "<template-dir/>");
        await projectDir.WriteAsync("model.xml",  "<project-root/>");

        var ctx = MakeContext(outputDir.Path, templateDir: templateDir.Path, projectRoot: projectDir.Path);

        ctx.ReadFile("model.xml").Should().Be("<template-dir/>");
    }

    [Fact]
    public void ReadFile_NotFoundAnywhere_ThrowsFileNotFoundException()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        var act = () => ctx.ReadFile("nonexistent.xml");

        act.Should().Throw<FileNotFoundException>()
            .WithMessage("*nonexistent.xml*");
    }

    [Fact]
    public void ReadFile_NotFoundAnywhere_ErrorListsSearchedLocations()
    {
        using var templateDir = new TempDirectory();
        using var projectDir  = new TempDirectory();
        using var outputDir   = new TempDirectory();

        var ctx = MakeContext(outputDir.Path, templateDir: templateDir.Path, projectRoot: projectDir.Path);

        var ex = Assert.Throws<FileNotFoundException>(() => ctx.ReadFile("missing.xml"));

        // Error should mention all three search locations
        ex.Message.Should().Contain(templateDir.Path);
        ex.Message.Should().Contain(projectDir.Path);
        ex.Message.Should().Contain(outputDir.Path);
    }

    [Fact]
    public void ReadFile_AbsolutePathNotFound_ThrowsFileNotFoundException()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        var act = () => ctx.ReadFile("/nonexistent/absolute/path.xml");

        act.Should().Throw<FileNotFoundException>();
    }

    [Fact]
    public void ReadFile_NullPath_ThrowsArgumentException()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        var act = () => ctx.ReadFile(null!);

        act.Should().Throw<ArgumentException>();
    }

    // ── PendingFiles ──────────────────────────────────────────────────────────

    [Fact]
    public void PendingFiles_ReflectsQueuedWriteFileCallsBeforeFlush()
    {
        using var dir = new TempDirectory();
        var ctx = MakeContext(dir.Path);

        ctx.WriteFile("a.txt", "a");
        ctx.WriteFile("b.txt", "b");

        ctx.PendingFiles.Should().HaveCount(2);
        ctx.PendingFiles.Should().Contain(f => f.RelativePath == "a.txt");
    }
}
