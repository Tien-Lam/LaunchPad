using LaunchDeck.Shared;

namespace LaunchDeck.Tests;

public class ConfigLoaderPathTests
{
    [Fact]
    public void GetDefaultConfigPath_EndsWithExpectedPath()
    {
        var path = ConfigLoader.GetDefaultConfigPath();
        Assert.EndsWith(@"LaunchDeck\config.json", path);
    }

    [Fact]
    public void StripPackagePath_RemovesPackagesSegment()
    {
        var input = @"C:\Users\testuser\AppData\Local\Packages\SomeApp_xyz\LocalState";
        var result = ConfigLoader.StripPackagePath(input);
        Assert.Equal(@"C:\Users\testuser\AppData\Local", result);
    }

    [Fact]
    public void StripPackagePath_LeavesNormalPathUnchanged()
    {
        var input = @"C:\Users\testuser\AppData\Local";
        var result = ConfigLoader.StripPackagePath(input);
        Assert.Equal(@"C:\Users\testuser\AppData\Local", result);
    }

    [Fact]
    public void StripPackagePath_IsCaseInsensitive()
    {
        var input = @"C:\Users\testuser\AppData\Local\packages\SomeApp_xyz\LocalState";
        var result = ConfigLoader.StripPackagePath(input);
        Assert.Equal(@"C:\Users\testuser\AppData\Local", result);
    }
}
