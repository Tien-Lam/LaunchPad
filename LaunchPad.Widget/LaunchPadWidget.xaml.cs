using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using LaunchPad.Shared;
using LaunchPad.Widget.Models;
using LaunchPad.Widget.Services;
using Microsoft.Gaming.XboxGameBar;

namespace LaunchPad.Widget;

public sealed partial class LaunchPadWidget : Page
{
    private bool _configUpdatedSubscribed;
    public ObservableCollection<LaunchItem> Items { get; } = new();

    public LaunchPadWidget()
    {
        this.InitializeComponent();
        this.Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        // Start companion process
        try
        {
            await FullTrustProcessLauncher.LaunchFullTrustProcessForCurrentAppAsync();
            // Give companion time to connect
            await Task.Delay(500);
        }
        catch (Exception)
        {
            // Companion may already be running
        }

        await LoadConfigAsync();

        if (!_configUpdatedSubscribed)
        {
            _configUpdatedSubscribed = true;
            CompanionClient.ConfigUpdated += async () =>
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    await LoadConfigAsync();
                });
            };
        }

        // Honor Game Bar opacity setting for compact/pinned mode
        // Apply to background only — not page Opacity, which washes out text and icons
        var widget = App.Widget;
        if (widget != null)
        {
            try
            {
                ApplyBackgroundOpacity(widget.RequestedOpacity / 100.0);
                widget.RequestedOpacityChanged += (opacitySender, args) =>
                {
                    _ = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                    {
                        ApplyBackgroundOpacity(opacitySender.RequestedOpacity / 100.0);
                    });
                };
            }
            catch
            {
                // RequestedOpacity may not be available in all Game Bar versions
            }
        }
    }

    private async Task LoadConfigAsync()
    {
        var (status, config, configPath, error) = await CompanionClient.LoadConfigAsync();
        var displayPath = configPath ?? ConfigLoader.GetDefaultConfigPath();

        if (status == ConfigLoadStatus.FileNotFound)
        {
            ShowEmptyState("No apps configured",
                "Click the gear button to add apps");
            return;
        }

        if (status == ConfigLoadStatus.ParseError)
        {
            ShowEmptyState("Invalid config file",
                $"JSON parse error:\n{error}");
            return;
        }

        if (config == null || config.Items.Count == 0)
        {
            ShowEmptyState("No apps configured",
                "Click the gear button to add apps");
            return;
        }

        Items.Clear();
        foreach (var item in config.Items)
        {
            var launchItem = new LaunchItem
            {
                Name = item.Name,
                Type = item.Type.ToString().ToLowerInvariant(),
                Path = item.Path,
                Args = item.Args,
                CustomIconPath = item.Icon
            };
            Items.Add(launchItem);
        }

        ItemsScrollViewer.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;

        await LoadIconsAsync();

        // Set initial focus to first tile for controller navigation
        if (Items.Count > 0)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Low, () =>
            {
                var firstContainer = ItemsGrid.ContainerFromIndex(0) as GridViewItem;
                firstContainer?.Focus(FocusState.Programmatic);
            });
        }
    }

    private async Task LoadIconsAsync()
    {
        foreach (var item in Items)
        {
            byte[]? iconData = null;

            // Custom icon takes priority over type-based extraction
            if (!string.IsNullOrEmpty(item.CustomIconPath))
            {
                iconData = await CompanionClient.LoadCustomIconAsync(item.CustomIconPath!);
            }

            // Fall back to type-based extraction
            if (iconData == null)
            {
                if (item.Type == "exe")
                {
                    iconData = await CompanionClient.ExtractIconAsync(item.Path);
                }
                else if (item.Type == "url")
                {
                    iconData = await CompanionClient.FetchFaviconAsync(item.Path);
                }
                else if (item.Type == "store")
                {
                    var aumid = ExtractAumidFromPath(item.Path);
                    if (aumid != null)
                        iconData = await CompanionClient.ExtractStoreIconAsync(aumid);
                }
            }

            if (iconData != null)
            {
                try
                {
                    var bitmap = new BitmapImage();
                    using (var stream = new Windows.Storage.Streams.InMemoryRandomAccessStream())
                    {
                        await stream.WriteAsync(iconData.AsBuffer());
                        stream.Seek(0);
                        await bitmap.SetSourceAsync(stream);
                    }
                    item.IconSource = bitmap;
                }
                catch
                {
                    SetDefaultIcon(item);
                }
            }
            else
            {
                SetDefaultIcon(item);
            }
        }
    }

    private void ApplyBackgroundOpacity(double opacity)
    {
        var alpha = (byte)(opacity * 255);
        this.Background = new SolidColorBrush(Windows.UI.Color.FromArgb(alpha, 0x20, 0x20, 0x20));
    }

    private static string? ExtractAumidFromPath(string path)
    {
        const string prefix = @"shell:AppsFolder\";
        if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return path.Substring(prefix.Length);
        return null;
    }

    private void SetDefaultIcon(LaunchItem item)
    {
        var assetName = item.Type == "url" ? "DefaultGlobe.png" : "DefaultApp.png";
        item.IconSource = new BitmapImage(new Uri($"ms-appx:///Assets/{assetName}"));
    }

    private void ShowEmptyState(string title, string message)
    {
        ItemsScrollViewer.Visibility = Visibility.Collapsed;
        EmptyState.Visibility = Visibility.Visible;
        EmptyStateTitle.Text = title;
        EmptyStateMessage.Text = message;
    }

    private async void OnItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is LaunchItem item)
        {
            var success = await CompanionClient.LaunchAsync(item.Type, item.Path, item.Args);

            if (sender is GridView gridView)
            {
                var container = gridView.ContainerFromItem(item) as GridViewItem;
                if (container != null)
                {
                    var overlay = FindChild<Border>(container, "FeedbackOverlay");
                    if (overlay != null)
                    {
                        overlay.Background = success
                            ? (SolidColorBrush)Resources["LaunchSuccessBrush"]
                            : (SolidColorBrush)Resources["LaunchFailureBrush"];

                        var fadeOut = new DoubleAnimation
                        {
                            From = 1.0,
                            To = 0.0,
                            Duration = new Duration(TimeSpan.FromMilliseconds(400)),
                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
                        };
                        Storyboard.SetTarget(fadeOut, overlay);
                        Storyboard.SetTargetProperty(fadeOut, "Opacity");
                        var sb = new Storyboard();
                        sb.Children.Add(fadeOut);
                        sb.Begin();
                    }
                }
            }
        }
    }

    private void OnTilePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
            AnimateTileBackground(grid, "#383838", TimeSpan.FromMilliseconds(150));
    }

    private void OnTilePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            AnimateTileBackground(grid, "#2D2D2D", TimeSpan.FromMilliseconds(150));
            AnimateTileScale(grid, 1.0, TimeSpan.FromMilliseconds(100));
        }
    }

    private void OnTilePointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            AnimateTileBackground(grid, "#252525", TimeSpan.FromMilliseconds(100));
            AnimateTileScale(grid, 0.95, TimeSpan.FromMilliseconds(100));
        }
    }

    private void OnTilePointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
        {
            AnimateTileBackground(grid, "#383838", TimeSpan.FromMilliseconds(100));
            AnimateTileScale(grid, 1.0, TimeSpan.FromMilliseconds(100));
        }
    }

    private async void OnEditClick(object sender, RoutedEventArgs e)
    {
        EditButton.IsEnabled = false;
        try
        {
            await CompanionClient.OpenEditorAsync();
        }
        finally
        {
            EditButton.IsEnabled = true;
        }
    }

    private static void AnimateTileBackground(Grid grid, string colorHex, TimeSpan duration)
    {
        var color = ParseHexColor(colorHex);
        var animation = new ColorAnimation
        {
            To = color,
            Duration = new Duration(duration),
            EnableDependentAnimation = true
        };
        Storyboard.SetTarget(animation, grid.Background);
        Storyboard.SetTargetProperty(animation, "Color");
        var sb = new Storyboard();
        sb.Children.Add(animation);
        sb.Begin();
    }

    private static void AnimateTileScale(Grid grid, double scale, TimeSpan duration)
    {
        if (grid.RenderTransform is CompositeTransform transform)
        {
            var scaleX = new DoubleAnimation { To = scale, Duration = new Duration(duration) };
            var scaleY = new DoubleAnimation { To = scale, Duration = new Duration(duration) };
            Storyboard.SetTarget(scaleX, transform);
            Storyboard.SetTargetProperty(scaleX, "ScaleX");
            Storyboard.SetTarget(scaleY, transform);
            Storyboard.SetTargetProperty(scaleY, "ScaleY");
            var sb = new Storyboard();
            sb.Children.Add(scaleX);
            sb.Children.Add(scaleY);
            sb.Begin();
        }
    }

    private static Windows.UI.Color ParseHexColor(string hex)
    {
        hex = hex.TrimStart('#');
        byte r = Convert.ToByte(hex.Substring(0, 2), 16);
        byte g = Convert.ToByte(hex.Substring(2, 2), 16);
        byte b = Convert.ToByte(hex.Substring(4, 2), 16);
        return Windows.UI.Color.FromArgb(255, r, g, b);
    }

    private static T? FindChild<T>(DependencyObject parent, string? name = null) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found)
            {
                if (name == null || (found is FrameworkElement fe && fe.Name == name))
                    return found;
            }
            var result = FindChild<T>(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
