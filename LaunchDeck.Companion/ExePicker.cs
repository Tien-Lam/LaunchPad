using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using LaunchDeck.Shared;

namespace LaunchDeck.Companion;

public static class ExePicker
{
    public static string GetDisplayName(string? exePath)
    {
        if (string.IsNullOrEmpty(exePath))
            return "Unknown";

        try
        {
            if (File.Exists(exePath))
            {
                var info = FileVersionInfo.GetVersionInfo(exePath);
                if (!string.IsNullOrWhiteSpace(info.FileDescription))
                    return info.FileDescription;
            }
        }
        catch { }

        return Path.GetFileNameWithoutExtension(exePath) ?? "Unknown";
    }

    public static void AppendToConfig(LaunchDeckConfig config, string exePath, string displayName)
    {
        if (config.Items.Any(i =>
            string.Equals(i.Path, exePath, StringComparison.OrdinalIgnoreCase)))
            return;

        config.Items.Add(new LaunchItemConfig
        {
            Name = displayName,
            Type = LaunchItemType.Exe,
            Path = exePath
        });
    }

    public static string? ShowPickerDialog()
    {
        string? selectedPath = null;

        // OpenFileDialog requires STA thread
        var thread = new Thread(() =>
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select an application",
                Filter = "Executables (*.exe)|*.exe",
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == DialogResult.OK)
                selectedPath = dialog.FileName;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        return selectedPath;
    }
}
