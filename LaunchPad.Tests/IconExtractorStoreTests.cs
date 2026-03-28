using System.Linq;
using LaunchPad.Companion;
using Xunit;

namespace LaunchPad.Tests;

public class IconExtractorStoreTests
{
    [Fact]
    public void ExtractStoreAppIcon_KnownApp_ReturnsIconData()
    {
        var apps = StoreAppEnumerator.GetInstalledApps();
        var appWithIcon = apps.FirstOrDefault(a => a.IconPath != null);

        if (appWithIcon == null)
            return; // Skip if no apps have resolvable icons

        var (success, data) = IconExtractor.ExtractStoreAppIcon(appWithIcon.Aumid);

        Assert.True(success);
        Assert.NotNull(data);
        Assert.True(data.Length > 0);
    }

    [Fact]
    public void ExtractStoreAppIcon_InvalidAumid_ReturnsFailure()
    {
        var (success, data) = IconExtractor.ExtractStoreAppIcon("NonExistent.Package_12345!App");

        Assert.False(success);
        Assert.Null(data);
    }

    [Fact]
    public void ExtractStoreAppIcon_MalformedAumid_ReturnsFailure()
    {
        var (success, data) = IconExtractor.ExtractStoreAppIcon("not-a-valid-aumid");

        Assert.False(success);
        Assert.Null(data);
    }
}
