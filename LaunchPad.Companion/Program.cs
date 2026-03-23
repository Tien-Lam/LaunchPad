using System;
using System.Threading;
using System.Threading.Tasks;
using LaunchPad.Shared;
using Windows.ApplicationModel;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;

namespace LaunchPad.Companion;

class Program
{
    private static AppServiceConnection? _connection;
    private static readonly ManualResetEvent ExitEvent = new(false);

    static async Task Main()
    {
        Console.WriteLine("[LaunchPad Companion] Starting...");

        // Companion connects as CLIENT to the widget's App Service.
        // The App Service connection is bidirectional: the widget (server side)
        // sends requests via SendMessageAsync, and the companion handles them
        // via RequestReceived. This is the standard Desktop Bridge pattern.
        _connection = new AppServiceConnection
        {
            AppServiceName = "com.launchpad.service",
            PackageFamilyName = Package.Current.Id.FamilyName
        };
        _connection.RequestReceived += OnRequestReceived;
        _connection.ServiceClosed += (_, _) =>
        {
            Console.WriteLine("[LaunchPad Companion] Service closed. Exiting.");
            ExitEvent.Set();
        };

        var status = await _connection.OpenAsync();
        if (status != AppServiceConnectionStatus.Success)
        {
            Console.WriteLine($"[LaunchPad Companion] Failed to connect: {status}");
            return;
        }

        Console.WriteLine("[LaunchPad Companion] Connected to App Service. Waiting for requests...");
        ExitEvent.WaitOne();
    }

    private static async void OnRequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
    {
        var deferral = args.GetDeferral();
        try
        {
            var message = args.Request.Message;
            var action = message["action"] as string;

            ValueSet response;
            switch (action)
            {
                case "launch":
                    response = HandleLaunch(message);
                    break;
                case "extract-icon":
                    response = HandleExtractIcon(message);
                    break;
                case "fetch-favicon":
                    response = await HandleFetchFaviconAsync(message);
                    break;
                case "add-exe":
                    response = HandleAddExe(message);
                    break;
                default:
                    response = new ValueSet { ["status"] = "error", ["error"] = $"Unknown action: {action}" };
                    break;
            }

            await args.Request.SendResponseAsync(response);
        }
        catch (Exception ex)
        {
            var errorResponse = new ValueSet { ["status"] = "error", ["error"] = ex.Message };
            await args.Request.SendResponseAsync(errorResponse);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private static ValueSet HandleLaunch(ValueSet message)
    {
        var type = message["type"] as string ?? "";
        var path = message["path"] as string ?? "";
        var args = message.ContainsKey("args") ? message["args"] as string : null;

        var (success, error) = LaunchHandler.Launch(type, path, args);

        var response = new ValueSet { ["status"] = success ? "ok" : "error" };
        if (error != null) response["error"] = error;
        return response;
    }

    private static ValueSet HandleExtractIcon(ValueSet message)
    {
        var path = message["path"] as string ?? "";
        var cacheDir = IconExtractor.GetIconCacheDir();
        var (success, iconPath) = IconExtractor.ExtractFromExe(path, cacheDir);

        var response = new ValueSet { ["status"] = success ? "ok" : "error" };
        if (iconPath != null) response["iconPath"] = iconPath;
        return response;
    }

    private static async Task<ValueSet> HandleFetchFaviconAsync(ValueSet message)
    {
        var url = message["url"] as string ?? "";
        var cacheDir = IconExtractor.GetIconCacheDir();
        var (success, iconPath) = await IconExtractor.FetchFaviconAsync(url, cacheDir);

        var response = new ValueSet { ["status"] = success ? "ok" : "error" };
        if (iconPath != null) response["iconPath"] = iconPath;
        return response;
    }

    private static ValueSet HandleAddExe(ValueSet message)
    {
        var configPath = message["configPath"] as string ?? ConfigLoader.GetDefaultConfigPath();

        var exePath = ExePicker.ShowPickerDialog();
        if (exePath == null)
            return new ValueSet { ["status"] = "cancelled" };

        var displayName = ExePicker.GetDisplayName(exePath);

        var loadResult = ConfigLoader.Load(configPath);
        var config = loadResult.Config ?? new LaunchPadConfig();

        ExePicker.AppendToConfig(config, exePath, displayName);
        ConfigLoader.Save(configPath, config);

        return new ValueSet
        {
            ["status"] = "ok",
            ["name"] = displayName,
            ["path"] = exePath
        };
    }
}
