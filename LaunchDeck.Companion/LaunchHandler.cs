using System;
using System.Diagnostics;

namespace LaunchDeck.Companion;

public static class LaunchHandler
{
    public static ProcessStartInfo BuildProcessStartInfo(string type, string path, string? args)
    {
        return type.ToLowerInvariant() switch
        {
            "exe" => new ProcessStartInfo
            {
                FileName = path,
                Arguments = args ?? "",
                UseShellExecute = true
            },
            "url" or "store" => new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            },
            _ => throw new ArgumentException($"Unknown launch type: {type}", nameof(type))
        };
    }

    public static (bool Success, string? Error, Process? Process) Launch(string type, string path, string? args)
    {
        try
        {
            var startInfo = BuildProcessStartInfo(type, path, args);
            var process = Process.Start(startInfo);
            return (true, null, process);
        }
        catch (Exception ex)
        {
            return (false, ex.Message, null);
        }
    }
}
