using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace DiscordQuestDecoy.Services;

public static class Win
{
    [DllImport("dwmapi.dll")]
    static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    /// <summary>Asks Windows for a dark title bar. Silently does nothing if unsupported.</summary>
    public static void DarkTitleBar(Window window)
    {
        try
        {
            var handle = new WindowInteropHelper(window).Handle;
            if (handle == IntPtr.Zero) return;
            int on = 1;
            DwmSetWindowAttribute(handle, 20, ref on, sizeof(int));
            DwmSetWindowAttribute(handle, 19, ref on, sizeof(int));
        }
        catch
        {
            // purely cosmetic
        }
    }

    public static void Open(string target)
    {
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Info("Open failed: " + ex.Message);
        }
    }
}
