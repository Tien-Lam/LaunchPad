using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LaunchPad.Shared;

public class LaunchPadConfig
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
    public LaunchPadConfig? Config { get; set; }
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
            var config = JsonSerializer.Deserialize<LaunchPadConfig>(json, options);
            return new ConfigLoadResult { Config = config, Status = ConfigLoadStatus.Success };
        }
        catch (JsonException ex)
        {
            return new ConfigLoadResult { Status = ConfigLoadStatus.ParseError, ErrorMessage = ex.Message };
        }
    }

    public static string GetDefaultConfigPath()
    {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        // UWP sandbox redirects to Packages\...\LocalState — strip to get real path
        var packagesIdx = localAppData.IndexOf(@"\Packages\", StringComparison.OrdinalIgnoreCase);
        if (packagesIdx >= 0)
            localAppData = localAppData.Substring(0, packagesIdx);

        return System.IO.Path.Combine(localAppData, "LaunchPad", "config.json");
    }
}
