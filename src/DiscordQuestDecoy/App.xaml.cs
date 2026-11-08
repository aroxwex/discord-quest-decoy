using System.Windows;
using DiscordQuestDecoy.Services;

namespace DiscordQuestDecoy;

public partial class App : Application
{
    Mutex? _mutex;
    bool _ownsMutex;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Stand-in mode: this copy of the app plays the role of the game.
        var args = e.Args;
        var sleeper = Array.IndexOf(args, "--sleeper");
        if (sleeper >= 0)
        {
            var title = sleeper + 1 < args.Length ? args[sleeper + 1] : "Game";
            var textIndex = Array.IndexOf(args, "--text");
            var text = textIndex >= 0 && textIndex + 1 < args.Length ? args[textIndex + 1] : "";
            new SleeperWindow(title, text).Show();
            return;
        }

        if (!AppPaths.EnsureDataDir(out var error))
        {
            MessageBox.Show(Loc.Instance.F("err_noaccess", AppPaths.BaseDir) + "\n\n" + error,
                AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown();
            return;
        }

        SettingsStore.Load();
        Loc.Instance.SetLanguage(SettingsStore.Current.Language);

        _mutex = new Mutex(true, "DiscordQuestDecoy.SingleInstance", out var created);
        _ownsMutex = created;
        if (!created)
        {
            MessageBox.Show(Loc.Instance["err_alreadyrunning"], AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        try
        {
            if (_mutex != null)
            {
                if (_ownsMutex) _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }
        catch
        {
            // ignore
        }

        base.OnExit(e);
    }
}
