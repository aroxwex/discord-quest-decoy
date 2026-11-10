using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using DiscordQuestDecoy.Models;

namespace DiscordQuestDecoy.Services;

/// <summary>
/// Reads the optional "extra-games.txt" next to the exe. Format: a game name on its own line,
/// followed by one executable path per line; blank lines separate games; # starts a comment.
/// A missing or broken file never causes an error, it just yields no extra games.
/// </summary>
public static class ExtraGamesService
{
    public static string FilePath => Path.Combine(AppPaths.BaseDir, "extra-games.txt");

    static readonly string[] TemplateLines =
    {
        "# Extra games for Discord Quest Decoy",
        "# One game per block: the name on its own line, then one executable path per line.",
        "# A leading ..\\ in a path is ignored. Blank lines separate games.",
        "# Lines starting with # are comments. This file is optional.",
        "#",
        "# Example:",
        "# My Game",
        "# ..\\My Game\\mygame.exe",
        "",
    };

    public static string Template => string.Join(Environment.NewLine, TemplateLines);

    public static List<Game> Load()
    {
        var byName = new Dictionary<string, Game>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(FilePath)) return new List<Game>();

            Game? current = null;
            foreach (var raw in File.ReadLines(FilePath))
            {
                var line = raw.Trim().TrimStart('\uFEFF');

                if (line.Length == 0)
                {
                    current = null;
                    continue;
                }

                if (line.StartsWith('#') || line.StartsWith(';')) continue;

                if (IsPathLine(line))
                {
                    if (current == null) continue;

                    foreach (var part in Regex.Split(line, @"\s+(?=\.\.[\\/])"))
                    {
                        var path = Normalize(part);
                        if (path != null && GameListService.ValidExe(path))
                        {
                            if (!current.Exes.Contains(path, StringComparer.OrdinalIgnoreCase))
                                current.Exes.Add(path);
                        }
                        else
                        {
                            Log.Info("Extra games: skipped path \"" + part.Trim() + "\" for " + current.Name);
                        }
                    }
                    continue;
                }

                if (!byName.TryGetValue(line, out var game))
                {
                    game = new Game { Name = line, Id = MakeId(line), Custom = true };
                    byName[line] = game;
                }
                current = game;
            }
        }
        catch (Exception ex)
        {
            Log.Info("Extra games file could not be read: " + ex.Message);
        }

        var list = byName.Values.Where(g => g.Exes.Count > 0).ToList();
        foreach (var g in list) g.SelectedExe = g.Exes[0];
        list.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        return list;
    }

    static bool IsPathLine(string line) =>
        line.StartsWith("..", StringComparison.Ordinal) || line.Contains('\\');

    static string? Normalize(string part)
    {
        var s = part.Trim().Replace('\\', '/');
        while (s.StartsWith("../", StringComparison.Ordinal)) s = s.Substring(3);
        s = s.TrimStart('/');
        s = Regex.Replace(s, "/{2,}", "/");
        return s.Length == 0 ? null : s;
    }

    /// <summary>Stable numeric id derived from the name (16 digits starting with 9, so it can't clash with Discord ids).</summary>
    static string MakeId(string name)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(name.Trim().ToLowerInvariant()));
        var n = BitConverter.ToUInt64(hash, 0) % 1_000_000_000_000_000UL;
        return "9" + n.ToString("D15");
    }
}
