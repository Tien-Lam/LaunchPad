using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace LaunchDeck.Companion;

public record StoreAppInfo(string Name, string Aumid, string? IconPath);

public static class StoreAppEnumerator
{
    public static List<StoreAppInfo> GetInstalledApps()
    {
        var pm = new PackageManager();
        var packages = pm.FindPackagesForUser("");
        var apps = new List<StoreAppInfo>();

        foreach (var package in packages)
        {
            try
            {
                if (package.IsFramework || package.IsResourcePackage)
                    continue;

                if (package.SignatureKind == PackageSignatureKind.System)
                    continue;

                var installPath = package.InstalledLocation.Path;
                var manifestPath = Path.Combine(installPath, "AppxManifest.xml");
                if (!File.Exists(manifestPath))
                    continue;

                var doc = XDocument.Load(manifestPath);
                var ns = doc.Root?.GetDefaultNamespace() ?? XNamespace.None;
                var uapNs = doc.Root?.GetNamespaceOfPrefix("uap")
                    ?? XNamespace.Get("http://schemas.microsoft.com/appx/manifest/uap/windows10");

                var applicationElements = doc.Descendants(ns + "Application");

                foreach (var appElement in applicationElements)
                {
                    var appId = appElement.Attribute("Id")?.Value;
                    if (string.IsNullOrEmpty(appId))
                        continue;

                    var aumid = $"{package.Id.FamilyName}!{appId}";

                    var displayName = package.DisplayName;
                    if (string.IsNullOrEmpty(displayName) || displayName.StartsWith("ms-resource:"))
                        displayName = package.Id.Name;

                    var visualElements = appElement.Element(uapNs + "VisualElements");
                    var logoRelative = visualElements?.Attribute("Square44x44Logo")?.Value
                        ?? doc.Descendants(ns + "Logo").FirstOrDefault()?.Value;

                    string? iconPath = null;
                    if (!string.IsNullOrEmpty(logoRelative))
                        iconPath = ResolveLogoPath(installPath, logoRelative);

                    apps.Add(new StoreAppInfo(displayName, aumid, iconPath));
                }
            }
            catch
            {
                // Skip packages that can't be read (permission issues, etc.)
            }
        }

        return apps
            .GroupBy(a => GetPackageFamilyName(a.Aumid))
            .Select(g => g.First())
            .OrderBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    internal static string? ResolveLogoPath(string installPath, string logoRelative)
    {
        var fullPath = Path.Combine(installPath, logoRelative);

        if (File.Exists(fullPath))
            return fullPath;

        var dir = Path.GetDirectoryName(fullPath);
        var baseName = Path.GetFileNameWithoutExtension(fullPath);
        var ext = Path.GetExtension(fullPath);

        if (dir == null) return null;

        string[] qualifiers = {
            ".targetsize-48", ".targetsize-32", ".targetsize-24",
            ".scale-200", ".scale-150", ".scale-100"
        };

        foreach (var qualifier in qualifiers)
        {
            var qualified = Path.Combine(dir, $"{baseName}{qualifier}{ext}");
            if (File.Exists(qualified))
                return qualified;
        }

        if (Directory.Exists(dir))
        {
            var candidates = Directory.GetFiles(dir, $"{baseName}*{ext}");
            if (candidates.Length > 0)
                return candidates[0];
        }

        return null;
    }

    public static string? GetPackageFamilyName(string aumid)
    {
        var exclamationIndex = aumid.IndexOf('!');
        if (exclamationIndex < 0) return null;
        return aumid[..exclamationIndex];
    }
}
