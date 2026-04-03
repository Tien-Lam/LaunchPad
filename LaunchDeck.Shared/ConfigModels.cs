using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LaunchDeck.Shared;

public class LaunchDeckConfig
{
    [JsonPropertyName("items")]
    public List<LaunchItemConfig> Items { get; set; } = new();
}

public class LaunchItemConfig
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public LaunchItemType Type { get; set; }

    [JsonPropertyName("path")]
    public string Path { get; set; } = "";

    [JsonPropertyName("args")]
    public string? Args { get; set; }

    [JsonPropertyName("icon")]
    public string? Icon { get; set; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LaunchItemType
{
    Exe,
    Url,
    Store
}

public class ConfigLoadResult
{
    public LaunchDeckConfig? Config { get; set; }
    public ConfigLoadStatus Status { get; set; }
    public string? ErrorMessage { get; set; }
}

public enum ConfigLoadStatus
{
    Success,
    FileNotFound,
    ParseError
}

public static class ConfigLoader
{
    public static ConfigLoadResult Load(string path)
    {
        if (!File.Exists(path))
            return new ConfigLoadResult { Status = ConfigLoadStatus.FileNotFound };

        try
        {
            var json = File.ReadAllText(path);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var config = JsonSerializer.Deserialize<LaunchDeckConfig>(json, options);
            return new ConfigLoadResult { Config = config, Status = ConfigLoadStatus.Success };
        }
        catch (JsonException ex)
        {
            return new ConfigLoadResult { Status = ConfigLoadStatus.ParseError, ErrorMessage = ex.Message };
        }
    }

    public static void Save(string path, LaunchDeckConfig config)
    {
        var dir = System.IO.Path.GetDirectoryName(path);
        if (dir != null && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
        var json = JsonSerializer.Serialize(config, options);
        File.WriteAllText(path, json);
    }

    internal static string StripPackagePath(string localAppData)
    {
        var packagesIdx = localAppData.IndexOf(@"\Packages\", StringComparison.OrdinalIgnoreCase);
        if (packagesIdx >= 0)
            localAppData = localAppData.Substring(0, packagesIdx);
        return localAppData;
    }

    public static string GetDefaultConfigPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        localAppData = StripPackagePath(localAppData);
        return System.IO.Path.Combine(localAppData, "LaunchDeck", "config.json");
    }

    /// <summary>
    /// Parses config JSON using JsonDocument instead of JsonSerializer.Deserialize.
    /// This exists because UWP's .NET Native AOT silently ignores [JsonPropertyName]
    /// attributes, causing JsonSerializer.Deserialize to produce empty objects.
    /// Handles both lowercase (companion output) and PascalCase property names.
    /// </summary>
    public static LaunchDeckConfig ParseJson(string json)
    {
        var config = new LaunchDeckConfig();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if ((root.TryGetProperty("items", out var itemsEl) ||
             root.TryGetProperty("Items", out itemsEl)) &&
            itemsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var itemEl in itemsEl.EnumerateArray())
            {
                var item = new LaunchItemConfig();

                if (itemEl.TryGetProperty("name", out var v) || itemEl.TryGetProperty("Name", out v))
                    item.Name = v.GetString() ?? "";
                if (itemEl.TryGetProperty("path", out v) || itemEl.TryGetProperty("Path", out v))
                    item.Path = v.GetString() ?? "";
                if (itemEl.TryGetProperty("args", out v) || itemEl.TryGetProperty("Args", out v))
                    item.Args = v.ValueKind == JsonValueKind.Null ? null : v.GetString();
                if (itemEl.TryGetProperty("icon", out v) || itemEl.TryGetProperty("Icon", out v))
                    item.Icon = v.ValueKind == JsonValueKind.Null ? null : v.GetString();

                if (itemEl.TryGetProperty("type", out v) || itemEl.TryGetProperty("Type", out v))
                {
                    var typeStr = v.GetString() ?? "";
                    if (Enum.TryParse<LaunchItemType>(typeStr, true, out var parsed))
                        item.Type = parsed;
                }

                config.Items.Add(item);
            }
        }

        return config;
    }
}
