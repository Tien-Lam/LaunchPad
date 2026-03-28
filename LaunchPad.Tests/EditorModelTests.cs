using System.IO;
using LaunchPad.Companion.Editor;
using LaunchPad.Shared;

namespace LaunchPad.Tests;

public class EditorModelTests
{
    [Fact]
    public void AddExe_AppendsItemAndSelectsIt()
    {
        var model = new EditorModel();
        model.AddExe(@"C:\app.exe", "My App");

        Assert.Single(model.Items);
        Assert.Equal("My App", model.Items[0].Name);
        Assert.Equal(LaunchItemType.Exe, model.Items[0].Type);
        Assert.Equal(@"C:\app.exe", model.Items[0].Path);
        Assert.Equal(0, model.SelectedIndex);
    }

    [Fact]
    public void AddUrl_AppendsDefaultUrlItem()
    {
        var model = new EditorModel();
        model.AddUrl();

        Assert.Single(model.Items);
        Assert.Equal("New URL", model.Items[0].Name);
        Assert.Equal(LaunchItemType.Url, model.Items[0].Type);
        Assert.Equal("https://", model.Items[0].Path);
        Assert.Equal(0, model.SelectedIndex);
    }

    [Fact]
    public void AddStore_AppendsStoreItemAndSelectsIt()
    {
        var model = new EditorModel();
        model.AddStore("Spotify", @"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify");

        Assert.Single(model.Items);
        Assert.Equal("Spotify", model.Items[0].Name);
        Assert.Equal(LaunchItemType.Store, model.Items[0].Type);
        Assert.Equal(@"shell:AppsFolder\SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify", model.Items[0].Path);
        Assert.Equal(0, model.SelectedIndex);
    }

    [Fact]
    public void AddMultiple_SelectsLastAdded()
    {
        var model = new EditorModel();
        model.AddExe(@"C:\first.exe", "First");
        model.AddExe(@"C:\second.exe", "Second");

        Assert.Equal(2, model.Items.Count);
        Assert.Equal(1, model.SelectedIndex);
    }

    [Fact]
    public void Remove_MiddleItem_ClampsSelection()
    {
        var model = new EditorModel();
        model.AddExe(@"C:\a.exe", "A");
        model.AddExe(@"C:\b.exe", "B");
        model.AddExe(@"C:\c.exe", "C");

        var result = model.Remove(1);

        Assert.True(result);
        Assert.Equal(2, model.Items.Count);
        Assert.Equal("A", model.Items[0].Name);
        Assert.Equal("C", model.Items[1].Name);
        Assert.Equal(1, model.SelectedIndex);
    }

    [Fact]
    public void Remove_LastItem_SelectsPrevious()
    {
        var model = new EditorModel();
        model.AddExe(@"C:\a.exe", "A");
        model.AddExe(@"C:\b.exe", "B");

        model.Remove(1);

        Assert.Single(model.Items);
        Assert.Equal(0, model.SelectedIndex);
    }

    [Fact]
    public void Remove_OnlyItem_SetsSelectionToNegativeOne()
    {
        var model = new EditorModel();
        model.AddExe(@"C:\a.exe", "A");

        model.Remove(0);

        Assert.Empty(model.Items);
        Assert.Equal(-1, model.SelectedIndex);
    }

    [Fact]
    public void Remove_InvalidIndex_ReturnsFalse()
    {
        var model = new EditorModel();
        model.AddExe(@"C:\a.exe", "A");

        Assert.False(model.Remove(-1));
        Assert.False(model.Remove(5));
        Assert.Single(model.Items);
    }

    [Fact]
    public void MoveUp_SwapsWithPrevious()
    {
        var model = new EditorModel();
        model.AddExe(@"C:\a.exe", "A");
        model.AddExe(@"C:\b.exe", "B");
        model.AddExe(@"C:\c.exe", "C");

        var result = model.MoveUp(2);

        Assert.True(result);
        Assert.Equal("A", model.Items[0].Name);
        Assert.Equal("C", model.Items[1].Name);
        Assert.Equal("B", model.Items[2].Name);
        Assert.Equal(1, model.SelectedIndex);
    }

    [Fact]
    public void MoveUp_FirstItem_ReturnsFalse()
    {
        var model = new EditorModel();
        model.AddExe(@"C:\a.exe", "A");
        model.AddExe(@"C:\b.exe", "B");

        Assert.False(model.MoveUp(0));
        Assert.Equal("A", model.Items[0].Name);
    }

    [Fact]
    public void MoveDown_SwapsWithNext()
    {
        var model = new EditorModel();
        model.AddExe(@"C:\a.exe", "A");
        model.AddExe(@"C:\b.exe", "B");
        model.AddExe(@"C:\c.exe", "C");

        var result = model.MoveDown(0);

        Assert.True(result);
        Assert.Equal("B", model.Items[0].Name);
        Assert.Equal("A", model.Items[1].Name);
        Assert.Equal("C", model.Items[2].Name);
        Assert.Equal(1, model.SelectedIndex);
    }

    [Fact]
    public void MoveDown_LastItem_ReturnsFalse()
    {
        var model = new EditorModel();
        model.AddExe(@"C:\a.exe", "A");
        model.AddExe(@"C:\b.exe", "B");

        Assert.False(model.MoveDown(1));
        Assert.Equal("B", model.Items[1].Name);
    }

    [Fact]
    public void Load_ReadsConfigFile()
    {
        var tempFile = Path.GetTempFileName();
        File.WriteAllText(tempFile, """{ "items": [{ "name": "Test", "type": "exe", "path": "test.exe" }] }""");

        try
        {
            var model = new EditorModel();
            model.Load(tempFile);

            Assert.Single(model.Items);
            Assert.Equal("Test", model.Items[0].Name);
            Assert.Equal(0, model.SelectedIndex);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Load_MissingFile_StartsEmpty()
    {
        var model = new EditorModel();
        model.Load(@"C:\nonexistent\config.json");

        Assert.Empty(model.Items);
        Assert.Equal(-1, model.SelectedIndex);
    }

    [Fact]
    public void Save_WritesConfigAndCallsCallback()
    {
        var tempFile = Path.GetTempFileName();
        var callbackCalled = false;

        try
        {
            var model = new EditorModel();
            model.AddExe(@"C:\app.exe", "App");
            model.AddUrl();
            model.Save(tempFile, () => callbackCalled = true);

            Assert.True(callbackCalled);

            var result = ConfigLoader.Load(tempFile);
            Assert.Equal(ConfigLoadStatus.Success, result.Status);
            Assert.Equal(2, result.Config!.Items.Count);
            Assert.Equal("App", result.Config.Items[0].Name);
            Assert.Equal("New URL", result.Config.Items[1].Name);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }

    [Fact]
    public void Save_NullCallback_DoesNotThrow()
    {
        var tempFile = Path.GetTempFileName();

        try
        {
            var model = new EditorModel();
            model.AddExe(@"C:\app.exe", "App");
            model.Save(tempFile);

            var result = ConfigLoader.Load(tempFile);
            Assert.Equal(ConfigLoadStatus.Success, result.Status);
        }
        finally
        {
            File.Delete(tempFile);
        }
    }
}
