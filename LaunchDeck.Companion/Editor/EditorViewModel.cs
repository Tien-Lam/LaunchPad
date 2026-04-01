using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using LaunchDeck.Shared;

namespace LaunchDeck.Companion.Editor;

public class EditorViewModel : INotifyPropertyChanged
{
    private readonly EditorModel _model = new();
    private readonly string _configPath;
    private readonly Action? _onSaved;

    public ObservableCollection<ItemViewModel> Items { get; } = new();

    public EditorViewModel(string configPath, Action? onSaved)
    {
        _configPath = configPath;
        _onSaved = onSaved;

        _model.Load(configPath);
        RebuildItems();

        SaveCommand = new RelayCommand(Save);
        AddExeCommand = new RelayCommand<string>(path => AddExe(path));
        AddUrlCommand = new RelayCommand(AddUrl);
        AddStoreCommand = new RelayCommand<(string Name, string Aumid)>(t => AddStore(t.Name, t.Aumid));
        DialogSaveCommand = new RelayCommand(DialogSave);
        DialogCancelCommand = new RelayCommand(DialogCancel);
    }

    public string ItemCountText => $"{Items.Count} item{(Items.Count == 1 ? "" : "s")}";

    public ICommand SaveCommand { get; }
    public ICommand AddExeCommand { get; }
    public ICommand AddUrlCommand { get; }
    public ICommand AddStoreCommand { get; }
    public ICommand DialogSaveCommand { get; }
    public ICommand DialogCancelCommand { get; }

    private bool _isDialogOpen;
    public bool IsDialogOpen
    {
        get => _isDialogOpen;
        set { _isDialogOpen = value; OnPropertyChanged(); OnPropertyChanged(nameof(DialogVisibility)); }
    }

    public Visibility DialogVisibility => IsDialogOpen ? Visibility.Visible : Visibility.Collapsed;

    private ItemViewModel? _editingItem;

    private string _editName = "";
    public string EditName
    {
        get => _editName;
        set { _editName = value; OnPropertyChanged(); }
    }

    private string _editTypeLabel = "";
    public string EditTypeLabel
    {
        get => _editTypeLabel;
        set { _editTypeLabel = value; OnPropertyChanged(); }
    }

    private string _editPath = "";
    public string EditPath
    {
        get => _editPath;
        set { _editPath = value; OnPropertyChanged(); }
    }

    private string _editArgs = "";
    public string EditArgs
    {
        get => _editArgs;
        set { _editArgs = value; OnPropertyChanged(); }
    }

    private string _editIcon = "";
    public string EditIcon
    {
        get => _editIcon;
        set { _editIcon = value; OnPropertyChanged(); }
    }

    private bool _editIsExe;
    public bool EditIsExe
    {
        get => _editIsExe;
        set { _editIsExe = value; OnPropertyChanged(); OnPropertyChanged(nameof(EditArgsVisibility)); OnPropertyChanged(nameof(EditBrowsePathVisibility)); }
    }

    public Visibility EditArgsVisibility => EditIsExe ? Visibility.Visible : Visibility.Collapsed;
    public Visibility EditBrowsePathVisibility => EditIsExe ? Visibility.Visible : Visibility.Collapsed;

    public void Edit(ItemViewModel item)
    {
        _editingItem = item;
        EditName = item.Name;
        EditTypeLabel = item.TypeLabel;
        EditPath = item.Path;
        EditArgs = item.Args;
        EditIcon = item.Icon;
        EditIsExe = item.IsExe;
        IsDialogOpen = true;
    }

    public void Delete(ItemViewModel item)
    {
        var index = Items.IndexOf(item);
        if (index < 0) return;
        _model.Remove(index);
        Items.RemoveAt(index);
        OnPropertyChanged(nameof(ItemCountText));
    }

    public void MoveUp(ItemViewModel item)
    {
        var index = Items.IndexOf(item);
        if (!_model.MoveUp(index)) return;
        Items.Move(index, index - 1);
    }

    public void MoveDown(ItemViewModel item)
    {
        var index = Items.IndexOf(item);
        if (!_model.MoveDown(index)) return;
        Items.Move(index, index + 1);
    }

    private void AddExe(string exePath)
    {
        var displayName = ExePicker.GetDisplayName(exePath);
        _model.AddExe(exePath, displayName);
        var vm = new ItemViewModel(_model.Items[_model.Items.Count - 1]);
        Items.Add(vm);
        OnPropertyChanged(nameof(ItemCountText));
    }

    public void AddUrl()
    {
        _model.AddUrl();
        var vm = new ItemViewModel(_model.Items[_model.Items.Count - 1]);
        Items.Add(vm);
        OnPropertyChanged(nameof(ItemCountText));
        Edit(vm);
    }

    private void AddStore(string name, string aumid)
    {
        var path = $@"shell:AppsFolder\{aumid}";
        _model.AddStore(name, path);
        var vm = new ItemViewModel(_model.Items[_model.Items.Count - 1]);
        Items.Add(vm);
        OnPropertyChanged(nameof(ItemCountText));
    }

    private void DialogSave()
    {
        if (_editingItem != null)
        {
            _editingItem.Name = EditName;
            _editingItem.Path = EditPath;
            _editingItem.Args = EditArgs;
            _editingItem.Icon = EditIcon;
        }
        IsDialogOpen = false;
        _editingItem = null;
    }

    private void DialogCancel()
    {
        IsDialogOpen = false;
        _editingItem = null;
    }

    public List<string> Validate() => _model.Validate();

    private void Save()
    {
        var errors = _model.Validate();
        if (errors.Count > 0)
        {
            var result = MessageBox.Show(
                string.Join("\n", errors) + "\n\nSave anyway?",
                "Validation warnings",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes)
                return;
        }

        _model.Save(_configPath, _onSaved);
    }

    private void RebuildItems()
    {
        Items.Clear();
        foreach (var config in _model.Items)
            Items.Add(new ItemViewModel(config));
        OnPropertyChanged(nameof(ItemCountText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
