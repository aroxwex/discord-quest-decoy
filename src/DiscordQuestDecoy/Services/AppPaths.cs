namespace DiscordQuestDecoy.Services;

public static class AppPaths
{
    public static readonly string BaseDir = ResolveBaseDir();

    public static string DataDir => Path.Combine(BaseDir, "data");
    public static string LanguagesDir => Path.Combine(BaseDir, "languages");
    public static string GamesDir => Path.Combine(DataDir, "games");
    public static string SettingsFile => Path.Combine(DataDir, "settings.json");
    public static string GameListFile => Path.Combine(DataDir, "gamelist.json");
    public static string LogFile => Path.Combine(DataDir, "log.txt");

    static string ResolveBaseDir()
    {
        var p = Environment.ProcessPath;
        if (!string.IsNullOrEmpty(p))
        {
            var d = Path.GetDirectoryName(p);
            if (!string.IsNullOrEmpty(d)) return d;
        }
        return AppContext.BaseDirectory;
    }

    public static bool EnsureDataDir(out string error)
    {
        try
        {
            Directory.CreateDirectory(DataDir);
            var test = Path.Combine(DataDir, ".write-test");
            File.WriteAllText(test, "ok");
            File.Delete(test);
            error = "";
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }
}
