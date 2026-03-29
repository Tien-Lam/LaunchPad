using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using LaunchPad.Shared;

namespace LaunchPad.Companion.Editor;

public class ItemViewModel : INotifyPropertyChanged
{
    private readonly LaunchItemConfig _config;

    public ItemViewModel(LaunchItemConfig config)
    {
        _config = config;
        LoadIconAsync();
    }

    public LaunchItemConfig Config => _config;

    public string Name
    {
        get => _config.Name;
        set { _config.Name = value; OnPropertyChanged(); }
    }

    public string TypeLabel => _config.Type.ToString().ToLowerInvariant();

    public string Path
    {
        get => _config.Path;
        set { _config.Path = value; OnPropertyChanged(); }
    }

    public string Args
    {
        get => _config.Args ?? "";
        set { _config.Args = string.IsNullOrWhiteSpace(value) ? null : value; OnPropertyChanged(); }
    }

    public string Icon
    {
        get => _config.Icon ?? "";
        set { _config.Icon = string.IsNullOrWhiteSpace(value) ? null : value; OnPropertyChanged(); }
    }

    public bool IsExe => _config.Type == LaunchItemType.Exe;
    public Visibility ArgsVisibility => IsExe ? Visibility.Visible : Visibility.Collapsed;
    public Visibility BrowsePathVisibility => IsExe ? Visibility.Visible : Visibility.Collapsed;

    private string? _iconImagePath;
    public string? IconImagePath
    {
        get => _iconImagePath;
        private set { _iconImagePath = value; OnPropertyChanged(); OnPropertyChanged(nameof(FallbackVisibility)); }
    }

    public Visibility FallbackVisibility =>
        string.IsNullOrEmpty(IconImagePath) ? Visibility.Visible : Visibility.Collapsed;

    public string FallbackText => _config.Type switch
    {
        LaunchItemType.Exe => "EXE",
        LaunchItemType.Url => "URL",
        _ => "APP"
    };

    private async void LoadIconAsync()
    {
        var path = await Task.Run(() => ResolveIcon());
        IconImagePath = path;
    }

    private string? ResolveIcon()
    {
        var cacheDir = IconExtractor.GetIconCacheDir();

        if (!string.IsNullOrEmpty(_config.Icon) && File.Exists(_config.Icon))
            return _config.Icon;

        if (_config.Type == LaunchItemType.Exe)
        {
            var (success, path) = IconExtractor.ExtractFromExe(_config.Path, cacheDir);
            if (success) return path;
        }
        else if (_config.Type == LaunchItemType.Url)
        {
            var task = IconExtractor.FetchFaviconAsync(_config.Path, cacheDir);
            var (success, path) = task.GetAwaiter().GetResult();
            if (success) return path;
        }
        else if (_config.Type == LaunchItemType.Store)
        {
            var aumid = ExtractAumidFromPath(_config.Path);
            if (aumid != null)
            {
                var cachePath = System.IO.Path.Combine(cacheDir, IconExtractor.GetCacheFileName(aumid));
                if (File.Exists(cachePath))
                    return cachePath;

                var (success, data) = IconExtractor.ExtractStoreAppIcon(aumid);
                if (success && data != null)
                {
                    try { File.WriteAllBytes(cachePath, data); }
                    catch (IOException) { }
                    return cachePath;
                }
            }
        }

        return null;
    }

    private static string? ExtractAumidFromPath(string path)
    {
        const string prefix = @"shell:AppsFolder\";
        if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return path.Substring(prefix.Length);
        return null;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
