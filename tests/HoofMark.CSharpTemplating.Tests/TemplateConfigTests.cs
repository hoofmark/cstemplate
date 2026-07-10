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

        config.Get("key").ShouldBe("value");
    }

    [Fact]
    public void LoadFrom_NoSiblingJson_ReturnsEmptyConfig()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(dir.File("T.template.cs"), "// template");

        var config = TemplateConfig.LoadFrom(dir.File("T.template.cs"));

        config.All.ShouldBeEmpty();
    }

    // ── Get(string) ───────────────────────────────────────────────────────────

    [Fact]
    public void Get_ExistingStringKey_ReturnsValue()
    {
        var config = TemplateConfig.Parse("""{"name":"Alice"}""");

        config.Get("name").ShouldBe("Alice");
    }

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        var config = TemplateConfig.Parse("""{"name":"Alice"}""");

        config.Get("nonexistent").ShouldBeNull();
    }

    [Fact]
    public void Get_IsCaseInsensitive()
    {
        var config = TemplateConfig.Parse("""{"Namespace":"MyApp"}""");

        config.Get("namespace").ShouldBe("MyApp");
        config.Get("NAMESPACE").ShouldBe("MyApp");
        config.Get("Namespace").ShouldBe("MyApp");
    }

    [Fact]
    public void Get_NumericValue_ReturnsStringRepresentation()
    {
        var config = TemplateConfig.Parse("""{"count":42}""");

        config.Get("count").ShouldBe("42");
    }

    // ── Get<T>() ──────────────────────────────────────────────────────────────

    [Fact]
    public void GetT_ExistingKey_DeserialisesValue()
    {
        var config = TemplateConfig.Parse("""{"items":["a","b","c"]}""");

        var items = config.Get<List<string>>("items");

        items.ShouldBeEquivalentTo(new List<string> {"a", "b", "c"});
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

        prop.ShouldNotBeNull();
        prop!.Name.ShouldBe("Id");
        prop.Type.ShouldBe("int");
    }

    [Fact]
    public void GetT_MissingKey_ReturnsDefault()
    {
        var config = TemplateConfig.Parse("""{"other":"value"}""");

        config.Get<List<string>>("nonexistent").ShouldBeNull();
    }

    // ── TryGet ────────────────────────────────────────────────────────────────

    [Fact]
    public void TryGet_ExistingKey_ReturnsTrueAndValue()
    {
        var config = TemplateConfig.Parse("""{"env":"production"}""");

        var found = config.TryGet("env", out var value);

        found.ShouldBeTrue();
        value.ShouldBe("production");
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalseAndNull()
    {
        var config = TemplateConfig.Parse("""{"env":"production"}""");

        var found = config.TryGet("missing", out var value);

        found.ShouldBeFalse();
        value.ShouldBeNull();
    }

    // ── All ───────────────────────────────────────────────────────────────────

    [Fact]
    public void All_ReturnsAllTopLevelKeys()
    {
        var config = TemplateConfig.Parse("""{"a":"1","b":"2","c":"3"}""");

        config.All.Keys.ToList().ShouldBeEquivalentTo(new List<string> {"a", "b", "c"});
    }

    // ── Error handling ────────────────────────────────────────────────────────

    [Fact]
    public void Parse_MalformedJson_ThrowsTemplateConfigException()
    {
        var act = () => TemplateConfig.Parse("{ not valid json }");

        act.ShouldThrow<TemplateConfigException>();
    }

    [Fact]
    public void Parse_JsonArray_ThrowsTemplateConfigException()
    {
        var act = () => TemplateConfig.Parse("""["not","an","object"]""");

        act.ShouldThrow<TemplateConfigException>()
            .WithMessage("*object*");
    }

    // ── GetConfigPath ─────────────────────────────────────────────────────────

    [Fact]
    public void GetConfigPath_CompoundExtension_StripsFullSuffix()
    {
        var result = TemplateConfig.GetConfigPath(@"C:\templates\MyTemplate.template.cs");

        result.ShouldBe(@"C:\templates\MyTemplate.json");
    }

    [Fact]
    public void GetConfigPath_SimpleExtension_ReplacesExtensionOnly()
    {
        var result = TemplateConfig.GetConfigPath(@"C:\templates\MyTemplate.cs");

        result.ShouldBe(@"C:\templates\MyTemplate.json");
    }

    [Fact]
    public void GetConfigPath_IsCaseInsensitive()
    {
        var result = TemplateConfig.GetConfigPath(@"C:\templates\MyTemplate.TEMPLATE.CS");

        result.ShouldBe(@"C:\templates\MyTemplate.json");
    }

    // ── LoadFrom (compound extension) ─────────────────────────────────────────

    [Fact]
    public void LoadFrom_CompoundExtension_FindsSiblingJsonByBaseName()
    {
        using var dir = new TempDirectory();
        File.WriteAllText(dir.File("T.template.cs"), "// template");
        File.WriteAllText(dir.File("T.json"), """{"key":"value"}""");

        var config = TemplateConfig.LoadFrom(dir.File("T.template.cs"));

        config.Get("key").ShouldBe("value");
    }

    [Fact]
    public void Empty_AllOperationsReturnSafeDefaults()
    {
        var config = TemplateConfig.Empty;

        config.Get("any").ShouldBeNull();
        config.Get<List<string>>("any").ShouldBeNull();
        config.TryGet("any", out _).ShouldBeFalse();
        config.All.ShouldBeEmpty();
    }

    // ── Helper type for deserialisation test ──────────────────────────────────

    private sealed record PropertyDef(string Name, string Type);
}
