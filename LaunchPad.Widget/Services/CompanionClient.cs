using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace LaunchPad.Widget.Services;

public static class CompanionClient
{
    public static async Task<bool> LaunchAsync(string type, string path, string? args = null)
    {
        var connection = App.CompanionConnection;
        if (connection == null) return false;

        var request = new ValueSet
        {
            ["action"] = "launch",
            ["type"] = type,
            ["path"] = path
        };
        if (args != null) request["args"] = args;

        var response = await connection.SendMessageAsync(request);
        if (response.Status != AppServiceResponseStatus.Success) return false;

        return response.Message["status"] as string == "ok";
    }

    public static async Task<string?> ExtractIconAsync(string exePath)
    {
        var connection = App.CompanionConnection;
        if (connection == null) return null;

        var request = new ValueSet
        {
            ["action"] = "extract-icon",
            ["path"] = exePath
        };

        var response = await connection.SendMessageAsync(request);
        if (response.Status != AppServiceResponseStatus.Success) return null;

        if (response.Message["status"] as string == "ok")
            return response.Message["iconPath"] as string;

        return null;
    }

    public static async Task<string?> FetchFaviconAsync(string url)
    {
        var connection = App.CompanionConnection;
        if (connection == null) return null;

        var request = new ValueSet
        {
            ["action"] = "fetch-favicon",
            ["url"] = url
        };

        var response = await connection.SendMessageAsync(request);
        if (response.Status != AppServiceResponseStatus.Success) return null;

        if (response.Message["status"] as string == "ok")
            return response.Message["iconPath"] as string;

        return null;
    }
}
