using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using LaunchPad.Shared;

namespace LaunchPad.Companion.Editor;

public partial class EditorWindow : Window
{
    private readonly string _configPath;
    private readonly Action? _onSaved;
    private List<LaunchItemConfig> _items = new();
    private int _previousIndex = -1;

    public EditorWindow(string configPath, Action? onSaved)
    {
        InitializeComponent();
        _configPath = configPath;
        _onSaved = onSaved;
        LoadItems();
    }

    private void LoadItems()
    {
        var result = ConfigLoader.Load(_configPath);
        _items = result.Config?.Items ?? new List<LaunchItemConfig>();
        RefreshList(-1);
    }

    private void RefreshList(int selectIndex)
    {
        ItemList.SelectionChanged -= OnItemSelectionChanged;
        ItemList.Items.Clear();
        foreach (var item in _items)
            ItemList.Items.Add(new ListBoxEntry(item.Name, item.Type.ToString().ToLowerInvariant()));

        if (_items.Count == 0)
        {
            EditPanel.Visibility = Visibility.Collapsed;
            _previousIndex = -1;
        }
        else
        {
            var idx = Math.Clamp(selectIndex, 0, _items.Count - 1);
            ItemList.SelectedIndex = idx;
            _previousIndex = idx;
            ShowItemInForm(idx);
        }
        ItemCountLabel.Text = $"{_items.Count} item{(_items.Count == 1 ? "" : "s")}";
        ItemList.SelectionChanged += OnItemSelectionChanged;
    }

    private void SyncFormToItem()
    {
        var idx = _previousIndex;
        if (idx < 0 || idx >= _items.Count) return;
        var item = _items[idx];
        item.Name = NameBox.Text;
        item.Path = PathBox.Text;
        item.Args = string.IsNullOrWhiteSpace(ArgsBox.Text) ? null : ArgsBox.Text;
        item.Icon = string.IsNullOrWhiteSpace(IconBox.Text) ? null : IconBox.Text;
    }

    private void ShowItemInForm(int index)
    {
        if (index < 0 || index >= _items.Count)
        {
            EditPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var item = _items[index];
        EditPanel.Visibility = Visibility.Visible;
        NameBox.Text = item.Name;
        TypeLabel.Text = item.Type.ToString().ToLowerInvariant();
        PathBox.Text = item.Path;
        ArgsBox.Text = item.Args ?? "";
        IconBox.Text = item.Icon ?? "";

        bool isExe = item.Type == LaunchItemType.Exe;
        ArgsPanel.Visibility = isExe ? Visibility.Visible : Visibility.Collapsed;
        BrowsePathButton.Visibility = isExe ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnItemSelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        SyncFormToItem();
        _previousIndex = ItemList.SelectedIndex;
        ShowItemInForm(ItemList.SelectedIndex);
    }

    private void OnAddExeClick(object sender, RoutedEventArgs e)
    {
        var exePath = ExePicker.ShowPickerDialog();
        if (exePath == null) return;

        var displayName = ExePicker.GetDisplayName(exePath);
        var newItem = new LaunchItemConfig
        {
            Name = displayName,
            Type = LaunchItemType.Exe,
            Path = exePath
        };
        SyncFormToItem();
        _items.Add(newItem);
        RefreshList(_items.Count - 1);
    }

    private void OnAddUrlClick(object sender, RoutedEventArgs e)
    {
        var newItem = new LaunchItemConfig
        {
            Name = "New URL",
            Type = LaunchItemType.Url,
            Path = "https://"
        };
        SyncFormToItem();
        _items.Add(newItem);
        RefreshList(_items.Count - 1);
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        var idx = ItemList.SelectedIndex;
        if (idx < 0 || idx >= _items.Count) return;
        _items.RemoveAt(idx);
        RefreshList(idx);
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        var idx = ItemList.SelectedIndex;
        if (idx <= 0) return;
        SyncFormToItem();
        (_items[idx], _items[idx - 1]) = (_items[idx - 1], _items[idx]);
        RefreshList(idx - 1);
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        var idx = ItemList.SelectedIndex;
        if (idx < 0 || idx >= _items.Count - 1) return;
        SyncFormToItem();
        (_items[idx], _items[idx + 1]) = (_items[idx + 1], _items[idx]);
        RefreshList(idx + 1);
    }

    private void OnBrowsePathClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select an application",
            Filter = "Executables (*.exe)|*.exe",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            PathBox.Text = dialog.FileName;
    }

    private void OnBrowseIconClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new System.Windows.Forms.OpenFileDialog
        {
            Title = "Select an icon",
            Filter = "Images (*.png;*.ico;*.jpg;*.bmp)|*.png;*.ico;*.jpg;*.bmp",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            IconBox.Text = dialog.FileName;
    }

    private void OnSaveClick(object sender, RoutedEventArgs e)
    {
        SyncFormToItem();
        var config = new LaunchPadConfig { Items = _items };
        ConfigLoader.Save(_configPath, config);
        _onSaved?.Invoke();
    }
}

internal record ListBoxEntry(string Name, string Type)
{
    public override string ToString() => $"{Name}  [{Type}]";
}
