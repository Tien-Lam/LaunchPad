using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LaunchDeck.Companion.Editor;

public partial class StoreAppPickerWindow : Window
{
    private List<StoreAppInfo> _allApps = new();
    public StoreAppInfo? SelectedApp { get; private set; }

    public StoreAppPickerWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await Task.Run(() =>
        {
            _allApps = StoreAppEnumerator.GetInstalledApps();
        });

        LoadingText.Visibility = Visibility.Collapsed;
        ApplyFilter();
        SearchBox.Focus();
    }

    private void ApplyFilter()
    {
        var filter = SearchBox.Text.Trim();
        var filtered = string.IsNullOrEmpty(filter)
            ? _allApps
            : _allApps.Where(a => a.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        AppList.Items.Clear();
        foreach (var app in filtered)
        {
            AppList.Items.Add(new PickerEntry(app.Name, app.Aumid, app.IconPath));
        }
    }

    private void OnSearchTextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        OkButton.IsEnabled = AppList.SelectedItem != null;
    }

    private void OnOkClick(object sender, RoutedEventArgs e)
    {
        ConfirmSelection();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void OnListDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (AppList.SelectedItem != null)
            ConfirmSelection();
    }

    private void ConfirmSelection()
    {
        if (AppList.SelectedItem is PickerEntry entry)
        {
            SelectedApp = new StoreAppInfo(entry.Name, entry.Aumid, entry.IconPath);
            DialogResult = true;
            Close();
        }
    }
}

internal record PickerEntry(string Name, string Aumid, string? IconPath)
{
    public Visibility FallbackVisibility =>
        string.IsNullOrEmpty(IconPath) ? Visibility.Visible : Visibility.Collapsed;
    public override string ToString() => Name;
}
