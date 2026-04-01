using LaunchDeck.Companion;
using Xunit;

namespace LaunchDeck.Tests;

public class ExePickerTests
{
    [Fact]
    public void GetDisplayName_Notepad_ReturnsFileDescription()
    {
        // notepad.exe has FileDescription "Notepad" on all Windows
        var name = ExePicker.GetDisplayName(@"C:\Windows\notepad.exe");
        Assert.Equal("Notepad", name);
    }

    [Fact]
    public void GetDisplayName_NonexistentExe_ReturnsFileNameWithoutExtension()
    {
        var name = ExePicker.GetDisplayName(@"C:\nonexistent\MyApp.exe");
        Assert.Equal("MyApp", name);
    }

    [Fact]
    public void GetDisplayName_NullPath_ReturnsUnknown()
    {
        var name = ExePicker.GetDisplayName(null);
        Assert.Equal("Unknown", name);
    }

    [Fact]
    public void AppendToConfig_AddsNewItem()
    {
        var config = new LaunchDeck.Shared.LaunchDeckConfig();
        ExePicker.AppendToConfig(config, @"C:\app.exe", "My App");

        Assert.Single(config.Items);
        Assert.Equal("My App", config.Items[0].Name);
        Assert.Equal(LaunchDeck.Shared.LaunchItemType.Exe, config.Items[0].Type);
        Assert.Equal(@"C:\app.exe", config.Items[0].Path);
    }

    [Fact]
    public void AppendToConfig_DoesNotAddDuplicate()
    {
        var config = new LaunchDeck.Shared.LaunchDeckConfig();
        ExePicker.AppendToConfig(config, @"C:\app.exe", "My App");
        ExePicker.AppendToConfig(config, @"C:\app.exe", "My App");

        Assert.Single(config.Items);
    }

    [Fact]
    public void AppendToConfig_DuplicateIsCaseInsensitive()
    {
        var config = new LaunchDeck.Shared.LaunchDeckConfig();
        ExePicker.AppendToConfig(config, @"C:\app.exe", "App");
        ExePicker.AppendToConfig(config, @"C:\APP.EXE", "App");

        Assert.Single(config.Items);
    }
}
