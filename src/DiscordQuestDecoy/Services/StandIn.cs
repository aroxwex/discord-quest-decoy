using System.Text.Json;
using DiscordQuestDecoy.Models;

namespace DiscordQuestDecoy.Services;

public static class StandIn
{
    /// <summary>
    /// True when this exe is one of the stand-ins sitting in "...\data\games\&lt;id&gt;\...". Opening such a file
    /// by hand then shows the stand-in window instead of starting a second copy of the app (which would
    /// create another data folder). Reads the main app's settings only to find the game name and language.
    /// </summary>
    public static bool TryDetect(string? exePath, out string title, out string text)
    {
        title = "";
        text = "";
        if (string.IsNullOrEmpty(exePath)) return false;

        const string marker = "\\data\\games\\";
        var index = exePath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0) return false;

        var rest = exePath.Substring(index + marker.Length);
        if (!rest.Contains('\\')) return false;

        var id = rest.Split('\\')[0];
        if (id.Length == 0 || !id.All(char.IsDigit)) return false;

        var root = exePath.Substring(0, index);
        var name = Path.GetFileNameWithoutExtension(exePath);
        var language = "en";

        try
        {
            var file = Path.Combine(root, "data", "settings.json");
            if (File.Exists(file))
            {
                var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(file), Json.Options);
                if (settings != null)
                {
                    language = settings.Language;
                    var game = settings.MyGames.FirstOrDefault(g => g.Id == id);
                    if (game != null && game.Name.Length > 0) name = game.Name;
                }
            }
        }
        catch
        {
            // fall back to the file name and English
        }

        Loc.Instance.SetLanguage(language);
        title = name;
        text = Loc.Instance["sleeper_text"];
        return true;
    }
}
