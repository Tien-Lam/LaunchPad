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
        var logPath = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LaunchPad", "companion.log");
        void Log(string msg)
        {
            try
            {
                var dir = System.IO.Path.GetDirectoryName(logPath);
                if (dir != null) System.IO.Directory.CreateDirectory(dir);
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
            }
            catch { }
        }

        Log("Main: start");

        // Acquire mutex with timeout — if previous instance is a zombie with a dead
        // connection, don't wait forever. Proceed anyway and let App Service sort it out.
        using var mutex = new Mutex(false, "Local\\LaunchPadCompanion");
        if (!mutex.WaitOne(2000))
        {
            Log("Main: mutex not acquired after 2s, proceeding anyway");
        }
        else
        {
            Log("Main: mutex acquired");
        }

        _connection = new AppServiceConnection
        {
            AppServiceName = "com.launchpad.service",
            PackageFamilyName = Package.Current.Id.FamilyName
        };
        _connection.RequestReceived += OnRequestReceived;
        _connection.ServiceClosed += (_, _) =>
        {
            Log("ServiceClosed fired, signaling exit");
            ExitEvent.Set();
        };

        var status = await _connection.OpenAsync();
        Log($"Main: OpenAsync returned {status}");
        if (status != AppServiceConnectionStatus.Success)
            return;

        Log("Main: waiting on ExitEvent");
        ExitEvent.WaitOne();
        Log("Main: exiting");
    }

    public static async void NotifyConfigUpdated()
    {
        var connection = _connection;
        if (connection == null) return;
        try
        {
            var message = new ValueSet { ["action"] = "config-updated" };
            await connection.SendMessageAsync(message);
        }
        catch { }
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
                case "load-config":
                    response = HandleLoadConfig(message);
                    break;
                case "open-editor":
                    response = HandleOpenEditor(message);
                    break;
                case "load-custom-icon":
                    response = HandleLoadCustomIcon(message);
                    break;
                case "extract-store-icon":
                    response = HandleExtractStoreIcon(message);
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

        var (success, error, process) = LaunchHandler.Launch(type, path, args);

        if (success && process != null)
            _ = NativeMethods.FocusProcessAsync(process);

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
        if (success && iconPath != null && System.IO.File.Exists(iconPath))
            response["iconData"] = Convert.ToBase64String(System.IO.File.ReadAllBytes(iconPath));
        return response;
    }

    private static async Task<ValueSet> HandleFetchFaviconAsync(ValueSet message)
    {
        var url = message["url"] as string ?? "";
        var cacheDir = IconExtractor.GetIconCacheDir();
        var (success, iconPath) = await IconExtractor.FetchFaviconAsync(url, cacheDir);

        var response = new ValueSet { ["status"] = success ? "ok" : "error" };
        if (success && iconPath != null && System.IO.File.Exists(iconPath))
            response["iconData"] = Convert.ToBase64String(System.IO.File.ReadAllBytes(iconPath));
        return response;
    }

    private static ValueSet HandleLoadConfig(ValueSet message)
    {
        var configPath = message.ContainsKey("configPath")
            ? message["configPath"] as string ?? ConfigLoader.GetDefaultConfigPath()
            : ConfigLoader.GetDefaultConfigPath();

        var result = ConfigLoader.Load(configPath);

        var response = new ValueSet
        {
            ["status"] = result.Status.ToString().ToLowerInvariant(),
            ["configPath"] = configPath
        };

        if (result.Status == ConfigLoadStatus.Success && result.Config != null)
        {
            var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = false };
            response["json"] = System.Text.Json.JsonSerializer.Serialize(result.Config, options);
        }

        if (result.ErrorMessage != null)
            response["error"] = result.ErrorMessage;

        return response;
    }

    private static ValueSet HandleLoadCustomIcon(ValueSet message)
    {
        var path = message["path"] as string ?? "";
        var (success, data) = IconExtractor.LoadCustomIcon(path);

        var response = new ValueSet { ["status"] = success ? "ok" : "error" };
        if (success && data != null)
            response["iconData"] = Convert.ToBase64String(data);
        return response;
    }

    private static ValueSet HandleExtractStoreIcon(ValueSet message)
    {
        var aumid = message["aumid"] as string ?? "";
        var (success, data) = IconExtractor.ExtractStoreAppIcon(aumid);

        var response = new ValueSet { ["status"] = success ? "ok" : "error" };
        if (success && data != null)
            response["iconData"] = Convert.ToBase64String(data);
        return response;
    }

    private static ValueSet HandleOpenEditor(ValueSet message)
    {
        var configPath = message.ContainsKey("configPath")
            ? message["configPath"] as string ?? ConfigLoader.GetDefaultConfigPath()
            : ConfigLoader.GetDefaultConfigPath();

        EditorManager.OpenEditor(configPath, NotifyConfigUpdated);
        return new ValueSet { ["status"] = "ok" };
    }
}
