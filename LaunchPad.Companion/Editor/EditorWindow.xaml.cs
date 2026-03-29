using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using LaunchPad.Shared;

namespace LaunchPad.Companion.Editor;

public partial class EditorWindow : Window
{
    private readonly string _configPath;
    private readonly Action? _onSaved;
    private readonly EditorModel _model = new();
    private int _previousIndex = -1;
    private Point _dragStartPoint;

    public EditorWindow(string configPath, Action? onSaved)
    {
        InitializeComponent();
        _configPath = configPath;
        _onSaved = onSaved;
        LoadItems();
    }

    private void LoadItems()
    {
        _model.Load(_configPath);
        RefreshList(_model.SelectedIndex);
    }

    private void RefreshList(int selectIndex)
    {
        ItemList.SelectionChanged -= OnItemSelectionChanged;
        ItemList.Items.Clear();
        var cacheDir = IconExtractor.GetIconCacheDir();
        foreach (var item in _model.Items)
        {
            var iconPath = ResolveIconPath(item, cacheDir);
            ItemList.Items.Add(new ListBoxEntry(item.Name, item.Type.ToString().ToLowerInvariant(), iconPath));
        }

        if (_model.Items.Count == 0)
        {
            EditPanel.Visibility = Visibility.Collapsed;
            _previousIndex = -1;
        }
        else
        {
            var idx = Math.Clamp(selectIndex, 0, _model.Items.Count - 1);
            ItemList.SelectedIndex = idx;
            _previousIndex = idx;
            ShowItemInForm(idx);
        }
        ItemCountLabel.Text = $"{_model.Items.Count} item{(_model.Items.Count == 1 ? "" : "s")}";
        ItemList.SelectionChanged += OnItemSelectionChanged;
    }

    private static string? ResolveIconPath(LaunchItemConfig item, string cacheDir)
    {
        if (!string.IsNullOrEmpty(item.Icon) && File.Exists(item.Icon))
            return item.Icon;

        if (item.Type == LaunchItemType.Exe)
        {
            var (success, path) = IconExtractor.ExtractFromExe(item.Path, cacheDir);
            if (success) return path;
        }
        else if (item.Type == LaunchItemType.Url)
        {
            var task = IconExtractor.FetchFaviconAsync(item.Path, cacheDir);
            var (success, path) = task.GetAwaiter().GetResult();
            if (success) return path;
        }
        else if (item.Type == LaunchItemType.Store)
        {
            var aumid = ExtractAumidFromPath(item.Path);
            if (aumid != null)
            {
                var cachePath = Path.Combine(cacheDir, IconExtractor.GetCacheFileName(aumid));
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
            return path[prefix.Length..];
        return null;
    }

    private void SyncFormToItem()
    {
        var idx = _previousIndex;
        if (idx < 0 || idx >= _model.Items.Count) return;
        var item = _model.Items[idx];
        item.Name = NameBox.Text;
        item.Path = PathBox.Text;
        item.Args = string.IsNullOrWhiteSpace(ArgsBox.Text) ? null : ArgsBox.Text;
        item.Icon = string.IsNullOrWhiteSpace(IconBox.Text) ? null : IconBox.Text;
    }

    private void ShowItemInForm(int index)
    {
        if (index < 0 || index >= _model.Items.Count)
        {
            EditPanel.Visibility = Visibility.Collapsed;
            return;
        }

        var item = _model.Items[index];
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
        SyncFormToItem();
        _model.AddExe(exePath, displayName);
        RefreshList(_model.SelectedIndex);
    }

    private void OnAddUrlClick(object sender, RoutedEventArgs e)
    {
        SyncFormToItem();
        _model.AddUrl();
        RefreshList(_model.SelectedIndex);
    }

    private void OnAddStoreClick(object sender, RoutedEventArgs e)
    {
        var picker = new StoreAppPickerWindow { Owner = this };
        if (picker.ShowDialog() == true && picker.SelectedApp != null)
        {
            var app = picker.SelectedApp;
            var path = $@"shell:AppsFolder\{app.Aumid}";
            SyncFormToItem();
            _model.AddStore(app.Name, path);
            RefreshList(_model.SelectedIndex);
        }
    }

    private void OnRemoveClick(object sender, RoutedEventArgs e)
    {
        var idx = ItemList.SelectedIndex;
        _model.Remove(idx);
        RefreshList(_model.SelectedIndex);
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        var idx = ItemList.SelectedIndex;
        SyncFormToItem();
        _model.MoveUp(idx);
        RefreshList(_model.SelectedIndex);
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        var idx = ItemList.SelectedIndex;
        SyncFormToItem();
        _model.MoveDown(idx);
        RefreshList(_model.SelectedIndex);
    }

    // -- Drag-and-drop reorder --

    private void OnListPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
    }

    private void OnListPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;

        var pos = e.GetPosition(null);
        var diff = _dragStartPoint - pos;

        if (Math.Abs(diff.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(diff.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        var listBox = (System.Windows.Controls.ListBox)sender;
        var sourceIndex = listBox.SelectedIndex;
        if (sourceIndex < 0) return;

        SyncFormToItem();
        DragDrop.DoDragDrop(listBox, sourceIndex, System.Windows.DragDropEffects.Move);
    }

    private void OnListDragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(typeof(int))
            ? System.Windows.DragDropEffects.Move
            : System.Windows.DragDropEffects.None;
        e.Handled = true;
    }

    private void OnListDrop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(int))) return;

        var sourceIndex = (int)e.Data.GetData(typeof(int))!;
        var listBox = (System.Windows.Controls.ListBox)sender;

        // Find drop target index
        var targetIndex = -1;
        var point = e.GetPosition(listBox);
        for (int i = 0; i < listBox.Items.Count; i++)
        {
            var container = (ListBoxItem?)listBox.ItemContainerGenerator.ContainerFromIndex(i);
            if (container == null) continue;
            var itemTop = container.TranslatePoint(new Point(0, 0), listBox).Y;
            var itemMid = itemTop + container.ActualHeight / 2;
            if (point.Y < itemMid)
            {
                targetIndex = i;
                break;
            }
        }
        if (targetIndex < 0) targetIndex = _model.Items.Count - 1;
        if (targetIndex == sourceIndex) return;

        // Move in model
        var item = _model.Items[sourceIndex];
        _model.Items.RemoveAt(sourceIndex);
        _model.Items.Insert(targetIndex, item);
        _model.SelectedIndex = targetIndex;

        RefreshList(targetIndex);
    }

    // -- Browse dialogs --

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

        var errors = _model.Validate();
        if (errors.Count > 0)
        {
            var result = System.Windows.MessageBox.Show(
                string.Join("\n", errors) + "\n\nSave anyway?",
                "Validation warnings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
        }

        _model.Save(_configPath, _onSaved);
    }
}

internal record ListBoxEntry(string Name, string Type, string? IconPath)
{
    public string TypeIcon => Type switch
    {
        "exe" => "EXE",
        "url" => "URL",
        _ => "APP"
    };
    public Visibility FallbackVisibility =>
        string.IsNullOrEmpty(IconPath) ? Visibility.Visible : Visibility.Collapsed;
    public override string ToString() => $"{Name}  [{Type}]";
}
