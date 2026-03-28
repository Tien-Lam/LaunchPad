using System;
using System.Windows;
using System.Windows.Forms;
using LaunchPad.Shared;

namespace LaunchPad.Companion.Editor;

public partial class EditorWindow : Window
{
    private readonly string _configPath;
    private readonly Action? _onSaved;
    private readonly EditorModel _model = new();
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
        _model.Load(_configPath);
        RefreshList(_model.SelectedIndex);
    }

    private void RefreshList(int selectIndex)
    {
        ItemList.SelectionChanged -= OnItemSelectionChanged;
        ItemList.Items.Clear();
        foreach (var item in _model.Items)
            ItemList.Items.Add(new ListBoxEntry(item.Name, item.Type.ToString().ToLowerInvariant()));

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
        _model.Save(_configPath, _onSaved);
    }
}

internal record ListBoxEntry(string Name, string Type)
{
    public string TypeIcon => Type switch
    {
        "exe" => "EXE",
        "url" => "URL",
        _ => "APP"
    };
    public override string ToString() => $"{Name}  [{Type}]";
}
