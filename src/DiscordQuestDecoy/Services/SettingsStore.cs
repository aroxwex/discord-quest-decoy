using System.Text.Json;
using DiscordQuestDecoy.Models;

namespace DiscordQuestDecoy.Services;

public static class SettingsStore
{
    public static AppSettings Current { get; private set; } = new();

    public static void Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(AppPaths.SettingsFile), Json.Options);
                if (s != null) Current = s;
            }
        }
        catch (Exception ex)
        {
            Log.Info("Settings load failed: " + ex.Message);
        }

        Current.MyGames ??= new List<Game>();
    }

    public static void Save()
    {
        try
        {
            var tmp = AppPaths.SettingsFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(Current, Json.Pretty));
            File.Move(tmp, AppPaths.SettingsFile, true);
        }
        catch (Exception ex)
        {
            Log.Info("Settings save failed: " + ex.Message);
        }
    }
}
