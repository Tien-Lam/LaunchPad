using System;
using System.Collections.Generic;
using LaunchPad.Shared;

namespace LaunchPad.Companion.Editor;

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

    public void Save(string configPath, Action? onSaved = null)
    {
        var config = new LaunchPadConfig { Items = Items };
        ConfigLoader.Save(configPath, config);
        onSaved?.Invoke();
    }
}
