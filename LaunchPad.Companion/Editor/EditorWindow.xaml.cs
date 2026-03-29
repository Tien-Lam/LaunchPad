using System;
using System.Windows;
using System.Windows.Input;

namespace LaunchPad.Companion.Editor;

public partial class EditorWindow : Window
{
    private EditorViewModel ViewModel => (EditorViewModel)DataContext;

    public EditorWindow(string configPath, Action? onSaved)
    {
        InitializeComponent();
        DataContext = new EditorViewModel(configPath, onSaved);
    }

    private void OnEditClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ItemViewModel item)
            ViewModel.Edit(item);
    }

    private void OnDeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ItemViewModel item)
            ViewModel.Delete(item);
    }

    private void OnMoveUpClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ItemViewModel item)
            ViewModel.MoveUp(item);
    }

    private void OnMoveDownClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is ItemViewModel item)
            ViewModel.MoveDown(item);
    }

    private void OnAddExeClick(object sender, RoutedEventArgs e)
    {
        var exePath = ExePicker.ShowPickerDialog();
        if (exePath != null)
            ViewModel.AddExeCommand.Execute(exePath);
    }

    private void OnAddUrlClick(object sender, RoutedEventArgs e)
    {
        ViewModel.AddUrlCommand.Execute(null);
    }

    private void OnAddStoreClick(object sender, RoutedEventArgs e)
    {
        var picker = new StoreAppPickerWindow { Owner = this };
        if (picker.ShowDialog() == true && picker.SelectedApp != null)
        {
            var app = picker.SelectedApp;
            ViewModel.AddStoreCommand.Execute((app.Name, app.Aumid));
        }
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
            ViewModel.EditPath = dialog.FileName;
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
            ViewModel.EditIcon = dialog.FileName;
    }

    private void OnBackdropClick(object sender, MouseButtonEventArgs e)
    {
        ViewModel.DialogCancelCommand.Execute(null);
    }
}
