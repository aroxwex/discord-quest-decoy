namespace DiscordQuestDecoy.Services;

public static class Log
{
    static readonly object Gate = new();

    public static void Info(string message)
    {
        try
        {
            lock (Gate)
            {
                var file = AppPaths.LogFile;
                if (File.Exists(file) && new FileInfo(file).Length > 512 * 1024)
                    File.Delete(file);
                File.AppendAllText(file, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // logging must never crash the app
        }
    }
}
