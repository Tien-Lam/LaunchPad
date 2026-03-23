using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml.Media.Imaging;

namespace LaunchPad.Widget.Models;

public class LaunchItem : INotifyPropertyChanged
{
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public string Path { get; set; } = "";
    public string? Args { get; set; }
    public string? CustomIconPath { get; set; }

    private BitmapImage? _iconSource;
    public BitmapImage? IconSource
    {
        get => _iconSource;
        set { _iconSource = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
