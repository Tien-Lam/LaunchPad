using System;
using System.Threading;
using System.Windows;

namespace LaunchPad.Companion;

public static class EditorManager
{
    private static Thread? _editorThread;
    private static Window? _editorWindow;
    private static readonly object Lock = new();

    public static bool IsEditorOpen
    {
        get
        {
            lock (Lock)
                return _editorThread != null && _editorThread.IsAlive;
        }
    }

    public static void OpenEditor(string configPath, Action? onSaved)
    {
        lock (Lock)
        {
            if (_editorThread != null && _editorThread.IsAlive)
            {
                _editorWindow?.Dispatcher.Invoke(() => _editorWindow.Activate());
                return;
            }

            _editorThread = new Thread(() =>
            {
                // WPF Application is required for Fluent theme and ThemeMode to work
                if (Application.Current == null)
                {
                    var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                    app.Resources.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/PresentationFramework.Fluent;component/Themes/Fluent.xaml")
                    });
#pragma warning disable WPF0001
                    app.ThemeMode = ThemeMode.Dark;
#pragma warning restore WPF0001
                }

                _editorWindow = new Editor.EditorWindow(configPath, onSaved);
                _editorWindow.Closed += (_, _) =>
                {
                    _editorWindow.Dispatcher.InvokeShutdown();
                    lock (Lock)
                    {
                        _editorWindow = null;
                        _editorThread = null;
                    }
                };
                _editorWindow.Show();
                System.Windows.Threading.Dispatcher.Run();
            });
            _editorThread.SetApartmentState(ApartmentState.STA);
            _editorThread.IsBackground = true;
            _editorThread.Start();
        }
    }
}
