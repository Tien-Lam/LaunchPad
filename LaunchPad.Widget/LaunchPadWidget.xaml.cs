using System;
using System.Collections.ObjectModel;
using System.Linq;
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

        CompanionClient.ConfigUpdated += async () =>
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                await LoadConfigAsync();
            });
        };
    }

    private async Task LoadConfigAsync()
    {
        var (status, config, configPath, error) = await CompanionClient.LoadConfigAsync();
        var displayPath = configPath ?? ConfigLoader.GetDefaultConfigPath();

        if (status == ConfigLoadStatus.FileNotFound)
        {
            ShowEmptyState("No config file found",
                $"Create a config.json at:\n{displayPath}");
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
                $"Add items to:\n{displayPath}");
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

        ItemsGrid.Visibility = Visibility.Visible;
        EmptyState.Visibility = Visibility.Collapsed;

        await LoadIconsAsync();
    }

    private async Task LoadIconsAsync()
    {
        foreach (var item in Items)
        {
            string? iconPath = null;

            if (item.CustomIconPath != null)
            {
                iconPath = item.CustomIconPath;
            }
            else if (item.Type == "exe")
            {
                iconPath = await CompanionClient.ExtractIconAsync(item.Path);
            }
            else if (item.Type == "url")
            {
                iconPath = await CompanionClient.FetchFaviconAsync(item.Path);
            }

            if (iconPath != null)
            {
                try
                {
                    var uri = new Uri(iconPath);
                    item.IconSource = new BitmapImage(uri);
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
        ItemsGrid.Visibility = Visibility.Collapsed;
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
