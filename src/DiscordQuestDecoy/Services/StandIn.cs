using System.Diagnostics;

namespace DiscordQuestDecoy.Services;

public static class StandIn
{
    public const string MutexName = "DiscordQuestDecoy.SingleInstance";

    /// <summary>
    /// True when this exe is one of the stand-ins sitting in "...\data\games\&lt;id&gt;\...".
    /// <paramref name="root"/> is the folder that contains the main app and its data folder.
    /// </summary>
    public static bool IsStandInPath(string? exePath, out string root)
    {
        root = "";
        if (string.IsNullOrEmpty(exePath)) return false;

        const string marker = "\\data\\games\\";
        var index = exePath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return false;

        var rest = exePath.Substring(index + marker.Length);
        if (!rest.Contains('\\')) return false;

        var id = rest.Split('\\')[0];
        if (id.Length == 0 || !id.All(char.IsDigit)) return false;

        root = exePath.Substring(0, index);
        return true;
    }

    /// <summary>
    /// A stand-in opened by hand just hands over to the main app: it starts it if it isn't running,
    /// and does nothing if it already is. Stand-ins are only meant to be started from the app itself.
    /// </summary>
    public static void OpenMainApp(string root)
    {
        try
        {
            if (Mutex.TryOpenExisting(MutexName, out var running))
            {
                running.Dispose();
                return;
            }

            var main = Path.Combine(root, "DiscordQuestDecoy.exe");
            if (File.Exists(main))
            {
                Process.Start(new ProcessStartInfo(main)
                {
                    UseShellExecute = true,
                    WorkingDirectory = root,
                });
            }
        }
        catch
        {
            // nothing sensible to do: just exit
        }
    }
}
