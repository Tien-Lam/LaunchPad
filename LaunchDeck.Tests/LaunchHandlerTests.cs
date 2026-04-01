using LaunchDeck.Companion;
using Xunit;

namespace LaunchDeck.Tests;

public class LaunchHandlerTests
{
    [Fact]
    public void BuildProcessStartInfo_Exe_SetsFileName()
    {
        var info = LaunchHandler.BuildProcessStartInfo("exe", @"C:\Windows\notepad.exe", null);

        Assert.Equal(@"C:\Windows\notepad.exe", info.FileName);
        Assert.Equal("", info.Arguments);
        Assert.True(info.UseShellExecute);
    }

    [Fact]
    public void BuildProcessStartInfo_ExeWithArgs_SetsArguments()
    {
        var info = LaunchHandler.BuildProcessStartInfo("exe", @"C:\app.exe", "--verbose --port 8080");

        Assert.Equal(@"C:\app.exe", info.FileName);
        Assert.Equal("--verbose --port 8080", info.Arguments);
    }

    [Fact]
    public void BuildProcessStartInfo_Url_SetsFileNameToUrl()
    {
        var info = LaunchHandler.BuildProcessStartInfo("url", "https://youtube.com", null);

        Assert.Equal("https://youtube.com", info.FileName);
        Assert.True(info.UseShellExecute);
    }

    [Fact]
    public void BuildProcessStartInfo_Store_SetsFileNameToProtocol()
    {
        var info = LaunchHandler.BuildProcessStartInfo("store", "spotify:", null);

        Assert.Equal("spotify:", info.FileName);
        Assert.True(info.UseShellExecute);
    }

    [Fact]
    public void BuildProcessStartInfo_UnknownType_ThrowsArgumentException()
    {
        Assert.Throws<System.ArgumentException>(
            () => LaunchHandler.BuildProcessStartInfo("unknown", "foo", null));
    }

    [Fact]
    public void Launch_InvalidPath_ReturnsFailureWithError()
    {
        var (success, error, _) = LaunchHandler.Launch("exe", @"C:\nonexistent\fake_app_12345.exe", null);

        Assert.False(success);
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("EXE")]
    [InlineData("Exe")]
    [InlineData("URL")]
    [InlineData("Store")]
    public void BuildProcessStartInfo_IsCaseInsensitive(string type)
    {
        var info = LaunchHandler.BuildProcessStartInfo(type, "test", null);
        Assert.True(info.UseShellExecute);
    }

    [Theory]
    [InlineData("url")]
    [InlineData("store")]
    public void BuildProcessStartInfo_UrlAndStore_IgnoresArgs(string type)
    {
        var info = LaunchHandler.BuildProcessStartInfo(type, "https://example.com", "--some-args");
        Assert.Equal("", info.Arguments);
    }
}
