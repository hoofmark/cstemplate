using System.Text.Json;
using HoofMark.CSharpTemplating.Core;
using HoofMark.CSharpTemplating.Tests.Helpers;

namespace HoofMark.CSharpTemplating.Tests;

public class NuGetResolverTests
{
    private readonly NuGetResolver _resolver = new();

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Writes a minimal but valid project.assets.json to a temp directory
    /// and returns the path to the file.
    /// </summary>
    private static async Task<string> WriteAssetsFile(
        TempDirectory dir,
        string targetFramework,
        Dictionary<string, (string[] compile, Dictionary<string, string>? deps)> packages,
        string packageFolder)
    {
        // Build targets section
        var targetEntries = new Dictionary<string, object>();
        foreach (var (key, (compile, deps)) in packages)
        {
            var entry = new Dictionary<string, object>
            {
                ["type"] = "package",
                ["compile"] = compile.ToDictionary(c => c, _ => (object)new { })
            };
            if (deps != null)
                entry["dependencies"] = deps;
            targetEntries[key] = entry;
        }

        // Build libraries section
        var libraries = packages.ToDictionary(
            p => p.Key,
            p => new { path = p.Key.Replace('/', Path.DirectorySeparatorChar).ToLowerInvariant() });

        var assets = new
        {
            version = 3,
            targets = new Dictionary<string, object> { [targetFramework] = targetEntries },
            libraries,
            packageFolders = new Dictionary<string, object> { [packageFolder] = new { } }
        };

        var path = dir.File("project.assets.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(assets));
        return path;
    }

    // ── Version check ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_UnsupportedVersion_ThrowsNuGetResolutionException()
    {
        using var dir = new TempDirectory();
        var path = dir.File("project.assets.json");
        await File.WriteAllTextAsync(path, 
            """
            { 
                "version": 99, 
                "targets": {}, 
                "libraries": {}, 
                "packageFolders": {} 
            }
            """, 
            cancellationToken: TestContext.Current.CancellationToken);

        var act = () => _resolver.Resolve(path, ["SomePackage"]);

        act.ShouldThrow<NuGetResolutionException>()
            .WithMessage("*Unsupported*version*99*");
        // act.Should().Throw<NuGetResolutionException>()
        //     .WithMessage("*Unsupported*version*99*");
    }

    [Fact]
    public async Task Resolve_SupportedVersion_DoesNotThrow()
    {
        using var dir = new TempDirectory();
        var path = dir.File("project.assets.json");
        await File.WriteAllTextAsync(path, 
            """
            {
              "version": 3,
              "targets": { "net10.0": {} },
              "libraries": {},
              "packageFolders": {}
            }
            """, 
			cancellationToken: TestContext.Current.CancellationToken);

        // Empty filter returns empty without throwing
        var act = () => _resolver.Resolve(path, []);

        act.ShouldNotThrow();
    }

    // ── TFM fallback chain ────────────────────────────────────────────

    [Fact]
    public async Task Resolve_ExactTfmMatch_ReturnsResult()
    {
        using var dir = new TempDirectory();
        var path = dir.File("project.assets.json");
        await File.WriteAllTextAsync(path,
            "{ \"version\": 3, \"targets\": { \"net10.0\": {} }, \"libraries\": {}, \"packageFolders\": {} }",
            cancellationToken: TestContext.Current.CancellationToken);

        var act = () => _resolver.Resolve(path, [], "net10.0");

        act.ShouldNotThrow();
    }

    [Fact]
    public async Task Resolve_OlderProjectTfm_FallsBackSuccessfully()
    {
        // Tool targets net10.0 but the project was restored for net8.0 --
        // resolver should fall back to the net8.0 target rather than failing.
        using var dir = new TempDirectory();
        var path = dir.File("project.assets.json");
        await File.WriteAllTextAsync(path,
            "{ \"version\": 3, \"targets\": { \"net8.0\": {} }, \"libraries\": {}, \"packageFolders\": {} }",
            cancellationToken: TestContext.Current.CancellationToken);

        var act = () => _resolver.Resolve(path, [], "net10.0");

        act.ShouldNotThrow();
    }

    [Fact]
    public async Task Resolve_IncompatibleTfm_ThrowsWithHelpfulMessage()
    {
        // Project restored for a future/incompatible TFM -- should give a
        // clear error listing available targets.
        using var dir = new TempDirectory();
        var path = dir.File("project.assets.json");
        await File.WriteAllTextAsync(path,
            "{ \"version\": 3, \"targets\": { \"net99.0\": {} }, \"libraries\": {}, \"packageFolders\": {} }",
            cancellationToken: TestContext.Current.CancellationToken);

        var act = () => _resolver.Resolve(path, ["SomePackage"], "net10.0");

        act.ShouldThrow<NuGetResolutionException>()
            .WithMessage("*Target framework*not found in*Available targets:*net99.0*");
    }

    // ── Missing assets file ───────────────────────────────────────────────────

    [Fact]
    public void Resolve_MissingAssetsFile_ThrowsWithHelpfulMessage()
    {
        var act = () => _resolver.Resolve("/nonexistent/project.assets.json", ["SomePackage"]);

        act.ShouldThrow<NuGetResolutionException>()
            .WithMessage("*dotnet restore*");
    }

    // ── Empty package filter ──────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_EmptyFilter_ReturnsEmpty()
    {
        using var dir = new TempDirectory();
        using var pkgDir = new TempDirectory();

        var assetsFile = await WriteAssetsFile(dir, "net10.0",
            new() { ["SomePackage/1.0.0"] = (["lib/net10.0/Some.dll"], null) },
            pkgDir.Path);

        var result = _resolver.Resolve(assetsFile, []);

        result.ManagedAssemblyPaths.ShouldBeEmpty();
    }

    // ── Basic resolution ──────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_SinglePackage_ReturnsAssemblyPath()
    {
        using var dir    = new TempDirectory();
        using var pkgDir = new TempDirectory();

        // Create the fake dll in the package folder
        var dllRelative = Path.Combine("somepackage", "1.0.0", "lib", "net10.0", "Some.dll");
        var dllFull     = Path.Combine(pkgDir.Path, dllRelative);
        Directory.CreateDirectory(Path.GetDirectoryName(dllFull)!);
        await File.WriteAllTextAsync(dllFull, "fake dll", cancellationToken: TestContext.Current.CancellationToken);

        var assetsFile = await WriteAssetsFile(dir, "net10.0",
            new() { ["SomePackage/1.0.0"] = (["lib/net10.0/Some.dll"], null) },
            pkgDir.Path + Path.DirectorySeparatorChar);

        var result = _resolver.Resolve(assetsFile, ["SomePackage"]);

        result.ManagedAssemblyPaths.ShouldContainSingle();
        result.ManagedAssemblyPaths[0].ShouldEndWith("Some.dll");
        File.Exists(result.ManagedAssemblyPaths[0]).ShouldBeTrue();
    }

    [Fact]
    public async Task Resolve_PackageNotInFilter_Excluded()
    {
        using var dir    = new TempDirectory();
        using var pkgDir = new TempDirectory();

        var dllFull = Path.Combine(pkgDir.Path, "included", "1.0.0", "lib", "net10.0", "Included.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(dllFull)!);
        await File.WriteAllTextAsync(dllFull, "fake", cancellationToken: TestContext.Current.CancellationToken);

        var assetsFile = await WriteAssetsFile(dir, "net10.0",
            new()
            {
                ["Included/1.0.0"] = (["lib/net10.0/Included.dll"], null),
                ["Excluded/1.0.0"] = (["lib/net10.0/Excluded.dll"], null),
            },
            pkgDir.Path + Path.DirectorySeparatorChar);

        var result = _resolver.Resolve(assetsFile, ["Included"]);

        result.ManagedAssemblyPaths.ShouldContainSingle();
        result.ManagedAssemblyPaths[0].ShouldEndWith("Included.dll");
    }

    // ── Transitive dependencies ───────────────────────────────────────────────

    [Fact]
    public async Task Resolve_TransitiveDependencies_Included()
    {
        using var dir    = new TempDirectory();
        using var pkgDir = new TempDirectory();

        // Root.dll depends on Transitive.dll
        foreach (var (pkg, dll) in new[]
        {
            ("root/1.0.0", "Root.dll"),
            ("transitive/1.0.0", "Transitive.dll")
        })
        {
            var dllPath = Path.Combine(pkgDir.Path, pkg, "lib", "net10.0", dll);
            Directory.CreateDirectory(Path.GetDirectoryName(dllPath)!);
            await File.WriteAllTextAsync(dllPath, "fake", cancellationToken: TestContext.Current.CancellationToken);
        }

        var assetsFile = await WriteAssetsFile(dir, "net10.0",
            new()
            {
                ["Root/1.0.0"]        = (["lib/net10.0/Root.dll"],
                                         new() { ["Transitive"] = "1.0.0" }),
                ["Transitive/1.0.0"]  = (["lib/net10.0/Transitive.dll"], null),
            },
            pkgDir.Path + Path.DirectorySeparatorChar);

        var result = _resolver.Resolve(assetsFile, ["Root"]);

        result.ManagedAssemblyPaths.ShouldHaveCount(2);
        result.ManagedAssemblyPaths.ShouldContain(p => p.EndsWith("Root.dll"));
        result.ManagedAssemblyPaths.ShouldContain(p => p.EndsWith("Transitive.dll"));
    }

    // ── Placeholder assemblies ────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_PlaceholderAssembly_Skipped()
    {
        using var dir    = new TempDirectory();
        using var pkgDir = new TempDirectory();

        // Some packages have "_._" as a placeholder — should be skipped
        var assetsFile = await WriteAssetsFile(dir, "net10.0",
            new() { ["Meta/1.0.0"] = (["lib/net10.0/_._"], null) },
            pkgDir.Path + Path.DirectorySeparatorChar);

        var result = _resolver.Resolve(assetsFile, ["Meta"]);

        result.ManagedAssemblyPaths.ShouldBeEmpty();
    }

    // ── Case insensitivity ────────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_FilterIsCaseInsensitive()
    {
        using var dir    = new TempDirectory();
        using var pkgDir = new TempDirectory();

        var dllFull = Path.Combine(pkgDir.Path, "mypackage", "1.0.0", "lib", "net10.0", "My.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(dllFull)!);
        await File.WriteAllTextAsync(dllFull, "fake", cancellationToken: TestContext.Current.CancellationToken);

        var assetsFile = await WriteAssetsFile(dir, "net10.0",
            new() { ["MyPackage/1.0.0"] = (["lib/net10.0/My.dll"], null) },
            pkgDir.Path + Path.DirectorySeparatorChar);

        // Filter uses different casing from the assets file
        var result = _resolver.Resolve(assetsFile, ["mypackage"]);

        result.ManagedAssemblyPaths.ShouldContainSingle();
    }

    // ── Missing assets target ─────────────────────────────────────────────────

    [Fact]
    public async Task Resolve_MalformedAssetsFile_ThrowsNuGetResolutionException()
    {
        using var dir = new TempDirectory();
        var path = dir.File("project.assets.json");
        await File.WriteAllTextAsync(path, "{ not valid json }", cancellationToken: TestContext.Current.CancellationToken);

        var act = () => _resolver.Resolve(path, ["SomePackage"]);

        act.ShouldThrow<NuGetResolutionException>();
    }
}
