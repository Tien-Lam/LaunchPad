using LaunchDeck.Companion;
using Xunit;

namespace LaunchDeck.Tests;

public class StoreAppEnumeratorTests
{
    [Fact]
    public void GetInstalledApps_ReturnsNonEmptyList()
    {
        var apps = StoreAppEnumerator.GetInstalledApps();
        Assert.NotEmpty(apps);
    }

    [Fact]
    public void GetInstalledApps_AllAppsHaveNameAndAumid()
    {
        var apps = StoreAppEnumerator.GetInstalledApps();
        foreach (var app in apps)
        {
            Assert.False(string.IsNullOrEmpty(app.Name), $"App with AUMID '{app.Aumid}' has no name");
            Assert.False(string.IsNullOrEmpty(app.Aumid), $"App '{app.Name}' has no AUMID");
            Assert.Contains("!", app.Aumid);
        }
    }

    [Fact]
    public void GetInstalledApps_ExcludesFrameworkPackages()
    {
        var apps = StoreAppEnumerator.GetInstalledApps();
        foreach (var app in apps)
        {
            Assert.DoesNotContain("Microsoft.NET", app.Aumid);
            Assert.DoesNotContain("Microsoft.VCLibs", app.Aumid);
        }
    }

    [Fact]
    public void GetInstalledApps_IsSortedByName()
    {
        var apps = StoreAppEnumerator.GetInstalledApps();
        for (int i = 1; i < apps.Count; i++)
        {
            Assert.True(
                string.Compare(apps[i - 1].Name, apps[i].Name, StringComparison.OrdinalIgnoreCase) <= 0,
                $"'{apps[i - 1].Name}' should come before '{apps[i].Name}'");
        }
    }

    [Fact]
    public void GetInstalledApps_ContainsKnownApp()
    {
        var apps = StoreAppEnumerator.GetInstalledApps();
        // At least one Microsoft app should exist on any Windows machine
        Assert.Contains(apps, a =>
            a.Aumid.Contains("Microsoft", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ParseAumid_ExtractsPackageFamilyName()
    {
        var pfn = StoreAppEnumerator.GetPackageFamilyName(
            "SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify");
        Assert.Equal("SpotifyAB.SpotifyMusic_zpdnekdrzrea0", pfn);
    }

    [Fact]
    public void ParseAumid_InvalidFormat_ReturnsNull()
    {
        var pfn = StoreAppEnumerator.GetPackageFamilyName("no-exclamation-mark");
        Assert.Null(pfn);
    }
}
