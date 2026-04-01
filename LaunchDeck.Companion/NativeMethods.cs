using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace LaunchDeck.Companion;

internal static class NativeMethods
{
    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(nint hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(nint hWnd, int nCmdShow);

    private const int SW_RESTORE = 9;

    internal static async Task FocusProcessAsync(Process process)
    {
        // Wait for the process to create its main window
        for (int i = 0; i < 20; i++)
        {
            await Task.Delay(100);
            process.Refresh();
            if (process.MainWindowHandle != nint.Zero)
            {
                ShowWindow(process.MainWindowHandle, SW_RESTORE);
                SetForegroundWindow(process.MainWindowHandle);
                return;
            }
        }
    }
}
