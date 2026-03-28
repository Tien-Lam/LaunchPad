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

            if (item.Type == "exe")
            {
                iconData = await CompanionClient.ExtractIconAsync(item.Path);
            }
            else if (item.Type == "url")
            {
                iconData = await CompanionClient.FetchFaviconAsync(item.Path);
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

            // Brief visual feedback: flash the clicked tile
            if (sender is GridView gridView)
            {
                var container = gridView.ContainerFromItem(item) as GridViewItem;
                if (container != null)
                {
                    var grid = FindChild<Grid>(container);
                    if (grid != null)
                    {
                        var originalBrush = grid.Background;
                        grid.Background = success
                            ? new SolidColorBrush(Windows.UI.Colors.Green) { Opacity = 0.3 }
                            : new SolidColorBrush(Windows.UI.Colors.Red) { Opacity = 0.3 };

                        await Task.Delay(200);
                        grid.Background = originalBrush;
                    }
                }
            }
        }
    }

    private void OnTilePointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
            grid.Background = (Brush)Resources["TileBackgroundHover"];
    }

    private void OnTilePointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (sender is Grid grid)
            grid.Background = (Brush)Resources["TileBackground"];
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

    private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T found) return found;
            var result = FindChild<T>(child);
            if (result != null) return result;
        }
        return null;
    }
}
