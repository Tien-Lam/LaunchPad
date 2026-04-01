using System.IO;
using LaunchDeck.Companion;
using Xunit;

namespace LaunchDeck.Tests;

public class IconExtractorTests
{
    [Fact]
    public void GetCacheFileName_ReturnsDeterministicHash()
    {
        var name1 = IconExtractor.GetCacheFileName(@"C:\Windows\notepad.exe");
        var name2 = IconExtractor.GetCacheFileName(@"C:\Windows\notepad.exe");

        Assert.Equal(name1, name2);
        Assert.EndsWith(".png", name1);
    }

    [Fact]
    public void GetCacheFileName_DifferentPaths_DifferentNames()
    {
        var name1 = IconExtractor.GetCacheFileName(@"C:\app1.exe");
        var name2 = IconExtractor.GetCacheFileName(@"C:\app2.exe");

        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public void ExtractFromExe_ValidExe_SavesPng()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "launchdeck-test-icons");
        Directory.CreateDirectory(cacheDir);

        try
        {
            // notepad.exe exists on all Windows installations
            var result = IconExtractor.ExtractFromExe(@"C:\Windows\notepad.exe", cacheDir);

            Assert.True(result.Success);
            Assert.NotNull(result.IconPath);
            Assert.True(File.Exists(result.IconPath));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void ExtractFromExe_NonexistentExe_ReturnsFailure()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "launchdeck-test-icons");
        Directory.CreateDirectory(cacheDir);

        try
        {
            var result = IconExtractor.ExtractFromExe(@"C:\nonexistent\app.exe", cacheDir);

            Assert.False(result.Success);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void GetFaviconUrl_ExtractsDomain()
    {
        var url = IconExtractor.GetFaviconUrl("https://www.youtube.com/watch?v=123");

        Assert.Contains("youtube.com", url);
    }
}
