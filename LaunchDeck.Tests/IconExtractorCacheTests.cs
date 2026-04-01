using System.IO;
using System.Threading.Tasks;
using LaunchDeck.Companion;

namespace LaunchDeck.Tests;

public class IconExtractorCacheTests
{
    [Fact]
    public void GetIconCacheDir_CreatesDirectory()
    {
        var dir = IconExtractor.GetIconCacheDir();

        Assert.True(Directory.Exists(dir));
        Assert.EndsWith("icons", dir);
    }

    [Fact]
    public void ExtractFromExe_CacheHit_ReturnsCachedFile()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "launchdeck-test-cache");
        Directory.CreateDirectory(cacheDir);

        try
        {
            var result1 = IconExtractor.ExtractFromExe(@"C:\Windows\notepad.exe", cacheDir);
            Assert.True(result1.Success);

            var cacheFile = result1.IconPath!;
            var cacheWriteTime = File.GetLastWriteTimeUtc(cacheFile);

            var result2 = IconExtractor.ExtractFromExe(@"C:\Windows\notepad.exe", cacheDir);
            Assert.True(result2.Success);
            Assert.Equal(cacheFile, result2.IconPath);
            Assert.Equal(cacheWriteTime, File.GetLastWriteTimeUtc(result2.IconPath!));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public async Task FetchFaviconAsync_FreshCache_ReturnsCachedFile()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "launchdeck-test-favicon-fresh");
        Directory.CreateDirectory(cacheDir);

        try
        {
            var url = "https://example.com";
            var cacheFile = Path.Combine(cacheDir, IconExtractor.GetCacheFileName(url));

            // Create a fresh cached file (within 7 days)
            File.WriteAllBytes(cacheFile, new byte[] { 0x01, 0x02, 0x03 });

            var (success, path) = await IconExtractor.FetchFaviconAsync(url, cacheDir);

            Assert.True(success);
            Assert.Equal(cacheFile, path);
            // Content unchanged — cache was used, not re-fetched
            Assert.Equal(new byte[] { 0x01, 0x02, 0x03 }, File.ReadAllBytes(cacheFile));
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public async Task FetchFaviconAsync_StaleCache_DoesNotReturnStaleFile()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "launchdeck-test-favicon-stale");
        Directory.CreateDirectory(cacheDir);

        try
        {
            var url = "https://definitely-not-a-real-domain-12345.invalid";
            var cacheFile = Path.Combine(cacheDir, IconExtractor.GetCacheFileName(url));

            // Create a stale cached file (older than 7 days)
            File.WriteAllBytes(cacheFile, new byte[] { 0x01, 0x02, 0x03 });
            File.SetLastWriteTimeUtc(cacheFile, DateTime.UtcNow.AddDays(-8));

            // Re-fetch will fail (invalid domain), so we get failure instead of stale data
            var (success, path) = await IconExtractor.FetchFaviconAsync(url, cacheDir);

            Assert.False(success);
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }
    }

    [Fact]
    public void ExtractFromExe_StaleCache_ReExtracts()
    {
        var cacheDir = Path.Combine(Path.GetTempPath(), "launchdeck-test-stale");
        Directory.CreateDirectory(cacheDir);

        try
        {
            // First extraction creates cache file
            var result1 = IconExtractor.ExtractFromExe(@"C:\Windows\notepad.exe", cacheDir);
            Assert.True(result1.Success);

            // Set cache file timestamp to the past (older than the EXE)
            File.SetLastWriteTimeUtc(result1.IconPath!, DateTime.UtcNow.AddDays(-365));
            var oldTime = File.GetLastWriteTimeUtc(result1.IconPath!);

            // Second extraction should re-extract (cache is stale)
            var result2 = IconExtractor.ExtractFromExe(@"C:\Windows\notepad.exe", cacheDir);
            Assert.True(result2.Success);

            var newTime = File.GetLastWriteTimeUtc(result2.IconPath!);
            Assert.True(newTime > oldTime, "Cache file should have been rewritten");
        }
        finally
        {
            if (Directory.Exists(cacheDir))
                Directory.Delete(cacheDir, true);
        }
    }
}
