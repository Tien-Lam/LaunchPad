using LaunchDeck.Shared;
using Xunit;

namespace LaunchDeck.Tests;

/// <summary>
/// Tests for ConfigLoader.ParseJson — the manual JsonDocument parser that replaces
/// JsonSerializer.Deserialize in the UWP widget. .NET Native's AOT compiler silently
/// ignores [JsonPropertyName] attributes, so Deserialize produces empty objects.
/// These tests verify that ParseJson correctly handles the JSON the companion actually
/// produces (lowercase property names) as well as PascalCase variants.
/// </summary>
public class ConfigParseTests
{
    [Fact]
    public void ParseJson_LowercaseProperties_ParsesCorrectly()
    {
        var json = """{"items":[{"name":"Test","type":"Exe","path":"C:\\test.exe"}]}""";

        var config = ConfigLoader.ParseJson(json);

        Assert.Single(config.Items);
        Assert.Equal("Test", config.Items[0].Name);
        Assert.Equal(LaunchItemType.Exe, config.Items[0].Type);
        Assert.Equal(@"C:\test.exe", config.Items[0].Path);
    }

    [Fact]
    public void ParseJson_PascalCaseProperties_ParsesCorrectly()
    {
        var json = """{"Items":[{"Name":"Test","Type":"Exe","Path":"C:\\test.exe"}]}""";

        var config = ConfigLoader.ParseJson(json);

        Assert.Single(config.Items);
        Assert.Equal("Test", config.Items[0].Name);
        Assert.Equal(LaunchItemType.Exe, config.Items[0].Type);
        Assert.Equal(@"C:\test.exe", config.Items[0].Path);
    }

    [Fact]
    public void ParseJson_ExeType_CaseInsensitive()
    {
        var json = """{"items":[{"name":"A","type":"exe","path":"a.exe"}]}""";
        Assert.Equal(LaunchItemType.Exe, ConfigLoader.ParseJson(json).Items[0].Type);

        json = """{"items":[{"name":"A","type":"EXE","path":"a.exe"}]}""";
        Assert.Equal(LaunchItemType.Exe, ConfigLoader.ParseJson(json).Items[0].Type);
    }

    [Fact]
    public void ParseJson_UrlType_CaseInsensitive()
    {
        var json = """{"items":[{"name":"A","type":"url","path":"https://x.com"}]}""";
        Assert.Equal(LaunchItemType.Url, ConfigLoader.ParseJson(json).Items[0].Type);

        json = """{"items":[{"name":"A","type":"Url","path":"https://x.com"}]}""";
        Assert.Equal(LaunchItemType.Url, ConfigLoader.ParseJson(json).Items[0].Type);
    }

    [Fact]
    public void ParseJson_StoreType_CaseInsensitive()
    {
        var json = """{"items":[{"name":"A","type":"store","path":"aumid"}]}""";
        Assert.Equal(LaunchItemType.Store, ConfigLoader.ParseJson(json).Items[0].Type);

        json = """{"items":[{"name":"A","type":"STORE","path":"aumid"}]}""";
        Assert.Equal(LaunchItemType.Store, ConfigLoader.ParseJson(json).Items[0].Type);
    }

    [Fact]
    public void ParseJson_AllThreeTypes_InOneConfig()
    {
        var json = """
        {
          "items": [
            {"name":"App","type":"Exe","path":"app.exe"},
            {"name":"Site","type":"Url","path":"https://example.com"},
            {"name":"Game","type":"Store","path":"Microsoft.Game_abc!App"}
          ]
        }
        """;

        var config = ConfigLoader.ParseJson(json);

        Assert.Equal(3, config.Items.Count);
        Assert.Equal(LaunchItemType.Exe, config.Items[0].Type);
        Assert.Equal(LaunchItemType.Url, config.Items[1].Type);
        Assert.Equal(LaunchItemType.Store, config.Items[2].Type);
    }

    [Fact]
    public void ParseJson_EmptyItemsArray_ReturnsEmptyConfig()
    {
        var json = """{"items":[]}""";

        var config = ConfigLoader.ParseJson(json);

        Assert.NotNull(config);
        Assert.Empty(config.Items);
    }

    [Fact]
    public void ParseJson_MissingItemsProperty_ReturnsEmptyConfig()
    {
        var json = """{"version":1}""";

        var config = ConfigLoader.ParseJson(json);

        Assert.NotNull(config);
        Assert.Empty(config.Items);
    }

    [Fact]
    public void ParseJson_NullItems_ReturnsEmptyConfig()
    {
        var json = """{"items":null}""";

        var config = ConfigLoader.ParseJson(json);

        Assert.NotNull(config);
        Assert.Empty(config.Items);
    }

    [Fact]
    public void ParseJson_OptionalFields_NullWhenAbsent()
    {
        var json = """{"items":[{"name":"Test","type":"Exe","path":"test.exe"}]}""";

        var item = ConfigLoader.ParseJson(json).Items[0];

        Assert.Null(item.Args);
        Assert.Null(item.Icon);
    }

    [Fact]
    public void ParseJson_OptionalFields_NullWhenExplicitlyNull()
    {
        var json = """{"items":[{"name":"Test","type":"Exe","path":"test.exe","args":null,"icon":null}]}""";

        var item = ConfigLoader.ParseJson(json).Items[0];

        Assert.Null(item.Args);
        Assert.Null(item.Icon);
    }

    [Fact]
    public void ParseJson_OptionalFields_PresentWhenProvided()
    {
        var json = """{"items":[{"name":"Discord","type":"Exe","path":"C:\\Discord\\Update.exe","args":"--processStart Discord.exe","icon":"C:\\icons\\discord.png"}]}""";

        var item = ConfigLoader.ParseJson(json).Items[0];

        Assert.Equal("--processStart Discord.exe", item.Args);
        Assert.Equal(@"C:\icons\discord.png", item.Icon);
    }

    [Fact]
    public void ParseJson_MultipleItems_AllParsed()
    {
        var json = """
        {
          "items": [
            {"name":"One","type":"Exe","path":"one.exe"},
            {"name":"Two","type":"Url","path":"https://two.com"},
            {"name":"Three","type":"Store","path":"three_aumid"},
            {"name":"Four","type":"Exe","path":"four.exe","args":"-v","icon":"four.ico"}
          ]
        }
        """;

        var config = ConfigLoader.ParseJson(json);

        Assert.Equal(4, config.Items.Count);
        Assert.Equal("One", config.Items[0].Name);
        Assert.Equal("Two", config.Items[1].Name);
        Assert.Equal("Three", config.Items[2].Name);
        Assert.Equal("Four", config.Items[3].Name);
        Assert.Equal("-v", config.Items[3].Args);
        Assert.Equal("four.ico", config.Items[3].Icon);
    }

    [Fact]
    public void ParseJson_MatchesJsonSerializerOutput()
    {
        // The companion serializes with JsonSerializer, which produces lowercase
        // property names due to [JsonPropertyName] attributes. Verify ParseJson
        // handles this exact format — this is the real-world failure case on .NET Native.
        var config = new LaunchDeckConfig
        {
            Items = new List<LaunchItemConfig>
            {
                new() { Name = "Notepad", Type = LaunchItemType.Exe, Path = @"C:\Windows\notepad.exe" },
                new() { Name = "Google", Type = LaunchItemType.Url, Path = "https://google.com" },
                new() { Name = "Xbox", Type = LaunchItemType.Store, Path = "Microsoft.Xbox_abc!App", Args = "--fast", Icon = "xbox.png" }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(config);
        var parsed = ConfigLoader.ParseJson(json);

        Assert.Equal(config.Items.Count, parsed.Items.Count);
        for (int i = 0; i < config.Items.Count; i++)
        {
            Assert.Equal(config.Items[i].Name, parsed.Items[i].Name);
            Assert.Equal(config.Items[i].Type, parsed.Items[i].Type);
            Assert.Equal(config.Items[i].Path, parsed.Items[i].Path);
            Assert.Equal(config.Items[i].Args, parsed.Items[i].Args);
            Assert.Equal(config.Items[i].Icon, parsed.Items[i].Icon);
        }
    }
}
