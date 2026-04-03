using System;
using System.Threading;
using System.Windows;

namespace LaunchDeck.Companion;

public static class EditorManager
{
    private static Thread? _staThread;
    private static System.Windows.Threading.Dispatcher? _dispatcher;
    private static Window? _editorWindow;
    private static readonly object Lock = new();

    private static void EnsureStaThread()
    {
        if (_staThread != null && _staThread.IsAlive && _dispatcher != null)
            return;

        var ready = new ManualResetEventSlim();
        _staThread = new Thread(() =>
        {
            if (Application.Current == null)
            {
                var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
#pragma warning disable WPF0001
                app.ThemeMode = ThemeMode.Dark;
#pragma warning restore WPF0001
            }
            _dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            ready.Set();
            System.Windows.Threading.Dispatcher.Run();
        });
        _staThread.SetApartmentState(ApartmentState.STA);
        _staThread.IsBackground = true;
        _staThread.Start();
        ready.Wait();
    }

    public static void OpenEditor(string configPath, Action? onSaved)
    {
        lock (Lock)
        {
            EnsureStaThread();

            _dispatcher!.Invoke(() =>
            {
                if (_editorWindow != null)
                {
                    Log.Write("EditorManager: editor already open, no-op");
                    return;
                }

                Log.Write("EditorManager: creating new window");
                _editorWindow = new Editor.EditorWindow(configPath, onSaved);
                _editorWindow.Closed += (_, _) =>
                {
                    Log.Write("EditorManager: window closed");
                    lock (Lock)
                        _editorWindow = null;
                };
                _editorWindow.Show();
            });
        }
    }
}
