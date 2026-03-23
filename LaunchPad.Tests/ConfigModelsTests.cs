using System.IO;
using System.Text.Json;
using LaunchPad.Shared;
using Xunit;

namespace LaunchPad.Tests;

public class ConfigModelsTests
{
    [Fact]
    public void Deserialize_ValidConfig_ReturnsItems()
    {
        var json = """
        {
          "items": [
            { "name": "Notepad", "type": "exe", "path": "C:\\Windows\\notepad.exe" },
            { "name": "Google", "type": "url", "path": "https://google.com" }
          ]
        }
        """;

        var config = JsonSerializer.Deserialize<LaunchPadConfig>(json);

        Assert.NotNull(config);
        Assert.Equal(2, config!.Items.Count);
        Assert.Equal("Notepad", config.Items[0].Name);
        Assert.Equal(LaunchItemType.Exe, config.Items[0].Type);
        Assert.Equal("C:\\Windows\\notepad.exe", config.Items[0].Path);
        Assert.Null(config.Items[0].Args);
        Assert.Null(config.Items[0].Icon);
    }

    [Fact]
    public void Deserialize_WithOptionalFields_ParsesCorrectly()
    {
        var json = """
        {
          "items": [
            {
              "name": "Discord",
              "type": "exe",
              "path": "C:\\Discord\\Update.exe",
              "args": "--processStart Discord.exe",
              "icon": "C:\\icons\\discord.png"
            }
          ]
        }
        """;

        var config = JsonSerializer.Deserialize<LaunchPadConfig>(json);

        Assert.Equal("--processStart Discord.exe", config!.Items[0].Args);
        Assert.Equal("C:\\icons\\discord.png", config.Items[0].Icon);
    }

    [Fact]
    public void Deserialize_EmptyItems_ReturnsEmptyList()
    {
        var json = """{ "items": [] }""";

        var config = JsonSerializer.Deserialize<LaunchPadConfig>(json);

        Assert.NotNull(config);
        Assert.Empty(config!.Items);
    }

    [Fact]
    public void Deserialize_AllTypes_ParsesCorrectly()
    {
        var json = """
        {
          "items": [
            { "name": "App", "type": "exe", "path": "app.exe" },
            { "name": "Site", "type": "url", "path": "https://example.com" },
            { "name": "Store", "type": "store", "path": "spotify:" }
          ]
        }
        """;

        var config = JsonSerializer.Deserialize<LaunchPadConfig>(json);

        Assert.Equal(LaunchItemType.Exe, config!.Items[0].Type);
        Assert.Equal(LaunchItemType.Url, config!.Items[1].Type);
        Assert.Equal(LaunchItemType.Store, config!.Items[2].Type);
    }

    [Fact]
    public void ConfigLoader_MissingFile_ReturnsFileNotFound()
    {
        var result = ConfigLoader.Load("C:\\nonexistent\\path\\config.json");
        Assert.Equal(ConfigLoadStatus.FileNotFound, result.Status);
        Assert.Null(result.Config);
    }

    [Fact]
    public void ConfigLoader_ValidFile_ReturnsSuccess()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, """{ "items": [{ "name": "Test", "type": "exe", "path": "test.exe" }] }""");

        try
        {
            var result = ConfigLoader.Load(tempFile);
            Assert.Equal(ConfigLoadStatus.Success, result.Status);
            Assert.NotNull(result.Config);
            Assert.Single(result.Config!.Items);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void ConfigLoader_MalformedJson_ReturnsParseError()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, "{ not valid json }}}");

        try
        {
            var result = ConfigLoader.Load(tempFile);
            Assert.Equal(ConfigLoadStatus.ParseError, result.Status);
            Assert.NotNull(result.ErrorMessage);
            Assert.Null(result.Config);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
