using FluentAssertions;
using HoofMark.CSharpTemplating.Core;
using HoofMark.CSharpTemplating.Tests.Helpers;

namespace HoofMark.CSharpTemplating.Tests;

public class TemplateConfigTests
{
    // ── LoadFrom ──────────────────────────────────────────────────────────────

    [Fact]
    public void LoadFrom_SiblingJsonExists_LoadsValues()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(dir.File("T.template.cs"), "// template");
        File.WriteAllText(dir.File("T.json"),        """{"key":"value"}""");

        var config = TemplateConfig.LoadFrom(dir.File("T.template.cs"));

        config.Get("key").Should().Be("value");
    }

    [Fact]
    public void LoadFrom_NoSiblingJson_ReturnsEmptyConfig()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(dir.File("T.template.cs"), "// template");

        var config = TemplateConfig.LoadFrom(dir.File("T.template.cs"));

        config.All.Should().BeEmpty();
    }

    // ── Get(string) ───────────────────────────────────────────────────────────

    [Fact]
    public void Get_ExistingStringKey_ReturnsValue()
    {
        var config = TemplateConfig.Parse("""{"name":"Alice"}""");

        config.Get("name").Should().Be("Alice");
    }

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        var config = TemplateConfig.Parse("""{"name":"Alice"}""");

        config.Get("nonexistent").Should().BeNull();
    }

    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var config = TemplateConfig.Parse("""{"Namespace":"MyApp"}""");

        config.Get("namespace").Should().Be("MyApp");
        config.Get("NAMESPACE").Should().Be("MyApp");
        config.Get("Namespace").Should().Be("MyApp");
    }

    [Fact]
    public void Get_NumericValue_ReturnsStringRepresentation()
    {
        var config = TemplateConfig.Parse("""{"count":42}""");

        config.Get("count").Should().Be("42");
    }

    // ── Get<T>() ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetT_ExistingKey_DeserialisesValue()
    {
        var config = TemplateConfig.Parse("""{"items":["a","b","c"]}""");

        var items = config.Get<List<string>>("items");

        items.Should().BeEquivalentTo(["a", "b", "c"]);
    }

    [Fact]
    public void GetT_ComplexObject_DeserialisesCorrectly()
    {
        var config = TemplateConfig.Parse("""
            {
              "property": { "name": "Id", "type": "int" }
            }
            """);

        var prop = config.Get<PropertyDef>("property");

        prop.Should().NotBeNull();
        prop!.Name.Should().Be("Id");
        prop.Type.Should().Be("int");
    }

    [Fact]
    public void GetT_MissingKey_ReturnsDefault()
    {
        var config = TemplateConfig.Parse("""{"other":"value"}""");

        config.Get<List<string>>("nonexistent").Should().BeNull();
    }

    // ── TryGet ────────────────────────────────────────────────────────────────

    [Fact]
    public void TryGet_ExistingKey_ReturnsTrueAndValue()
    {
        var config = TemplateConfig.Parse("""{"env":"production"}""");

        var found = config.TryGet("env", out var value);

        found.Should().BeTrue();
        value.Should().Be("production");
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalseAndNull()
    {
        var config = TemplateConfig.Parse("""{"env":"production"}""");

        var found = config.TryGet("missing", out var value);

        found.Should().BeFalse();
        value.Should().BeNull();
    }

    // ── All ───────────────────────────────────────────────────────────────────

    [Fact]
    public void All_ReturnsAllTopLevelKeys()
    {
        var config = TemplateConfig.Parse("""{"a":"1","b":"2","c":"3"}""");

        config.All.Keys.Should().BeEquivalentTo(["a", "b", "c"]);
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MalformedJson_ThrowsTemplateConfigException()
    {
        var act = () => TemplateConfig.Parse("{ not valid json }");

        act.Should().Throw<TemplateConfigException>();
    }

    [Fact]
    public void Parse_JsonArray_ThrowsTemplateConfigException()
    {
        var act = () => TemplateConfig.Parse("""["not","an","object"]""");

        act.Should().Throw<TemplateConfigException>()
            .WithMessage("*object*");
    }

    // ── GetConfigPath ─────────────────────────────────────────────────────────

    [Fact]
    public void GetConfigPath_CompoundExtension_StripsFullSuffix()
    {
        var result = TemplateConfig.GetConfigPath(@"C:\templates\MyTemplate.template.cs");

        result.Should().Be(@"C:\templates\MyTemplate.json");
    }

    [Fact]
    public void GetConfigPath_SimpleExtension_ReplacesExtensionOnly()
    {
        var result = TemplateConfig.GetConfigPath(@"C:\templates\MyTemplate.cs");

        result.Should().Be(@"C:\templates\MyTemplate.json");
    }

    [Fact]
    public void GetConfigPath_IsCaseInsensitive()
    {
        var result = TemplateConfig.GetConfigPath(@"C:\templates\MyTemplate.TEMPLATE.CS");

        result.Should().Be(@"C:\templates\MyTemplate.json");
    }

    // ── LoadFrom (compound extension) ─────────────────────────────────────────

    [Fact]
    public void LoadFrom_CompoundExtension_FindsSiblingJsonByBaseName()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(dir.File("T.template.cs"), "// template");
        File.WriteAllText(dir.File("T.json"), """{"key":"value"}""");

        var config = TemplateConfig.LoadFrom(dir.File("T.template.cs"));

        config.Get("key").Should().Be("value");
    }

    [Fact]
    public void Empty_AllOperationsReturnSafeDefaults()
    {
        var config = TemplateConfig.Empty;

        config.Get("any").Should().BeNull();
        config.Get<List<string>>("any").Should().BeNull();
        config.TryGet("any", out _).Should().BeFalse();
        config.All.Should().BeEmpty();
    }

    // ── Helper type for deserialisation test ──────────────────────────────────

    private sealed record PropertyDef(string Name, string Type);
}
