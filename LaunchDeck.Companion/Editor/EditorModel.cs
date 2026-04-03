using System;
using System.Collections.Generic;
using LaunchDeck.Shared;

namespace LaunchDeck.Companion.Editor;

public class EditorModel
{
    public List<LaunchItemConfig> Items { get; private set; } = new();
    public int SelectedIndex { get; set; } = -1;

    public void Load(string configPath)
    {
        var result = ConfigLoader.Load(configPath);
        Items = result.Config?.Items ?? new List<LaunchItemConfig>();
        SelectedIndex = Items.Count > 0 ? 0 : -1;
    }

    public void AddExe()
    {
        Items.Add(new LaunchItemConfig
        {
            Name = "New App",
            Type = LaunchItemType.Exe,
            Path = ""
        });
        SelectedIndex = Items.Count - 1;
    }

    public void AddExe(string path, string displayName)
    {
        Items.Add(new LaunchItemConfig
        {
            Name = displayName,
            Type = LaunchItemType.Exe,
            Path = path
        });
        SelectedIndex = Items.Count - 1;
    }

    public void AddUrl()
    {
        Items.Add(new LaunchItemConfig
        {
            Name = "New URL",
            Type = LaunchItemType.Url,
            Path = "https://"
        });
        SelectedIndex = Items.Count - 1;
    }

    public void AddStore(string name, string path)
    {
        Items.Add(new LaunchItemConfig
        {
            Name = name,
            Type = LaunchItemType.Store,
            Path = path
        });
        SelectedIndex = Items.Count - 1;
    }

    public bool Remove(int index)
    {
        if (index < 0 || index >= Items.Count) return false;
        Items.RemoveAt(index);
        SelectedIndex = Items.Count == 0 ? -1 : Math.Clamp(index, 0, Items.Count - 1);
        return true;
    }

    public bool MoveUp(int index)
    {
        if (index <= 0 || index >= Items.Count) return false;
        (Items[index], Items[index - 1]) = (Items[index - 1], Items[index]);
        SelectedIndex = index - 1;
        return true;
    }

    public bool MoveDown(int index)
    {
        if (index < 0 || index >= Items.Count - 1) return false;
        (Items[index], Items[index + 1]) = (Items[index + 1], Items[index]);
        SelectedIndex = index + 1;
        return true;
    }

    public List<string> Validate()
    {
        var errors = new List<string>();
        for (int i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            var label = $"Item {i + 1} '{item.Name}'";

            if (string.IsNullOrWhiteSpace(item.Name))
                errors.Add($"Item {i + 1} has an empty name.");

            if (string.IsNullOrWhiteSpace(item.Path))
            {
                errors.Add($"{label} has an empty path.");
                continue;
            }

            if (item.Type == LaunchItemType.Url &&
                !item.Path.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !item.Path.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} URL must start with http:// or https://");
            }

            if (item.Type == LaunchItemType.Store &&
                !item.Path.StartsWith(@"shell:AppsFolder\", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"{label} Store path must start with shell:AppsFolder\\");
            }
        }
        return errors;
    }

    public void Save(string configPath, Action? onSaved = null)
    {
        var config = new LaunchDeckConfig { Items = Items };
        Log.Write($"EditorModel.Save: path={configPath} items={Items.Count}");
        try
        {
            ConfigLoader.Save(configPath, config);
        }
        catch (Exception ex)
        {
            Log.Write($"EditorModel.Save: FAILED — {ex.Message}");
            throw;
        }
        onSaved?.Invoke();
    }
}
