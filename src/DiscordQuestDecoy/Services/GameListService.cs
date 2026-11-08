using System.Text.Json;
using DiscordQuestDecoy.Models;

namespace DiscordQuestDecoy.Services;

public sealed class GameListService
{
    const string DetectableUrl = "https://discord.com/api/applications/detectable";

    static readonly HttpClient Http = CreateClient();

    static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(40) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("DiscordQuestDecoy/1.0");
        return client;
    }

    /// <summary>Cached list if present, otherwise the list built into the exe.</summary>
    public GameListData LoadLocal()
    {
        try
        {
            if (File.Exists(AppPaths.GameListFile))
            {
                using var stream = File.OpenRead(AppPaths.GameListFile);
                var data = JsonSerializer.Deserialize<GameListData>(stream, Json.Options);
                if (data != null && data.Games.Count > 100) return data;
            }
        }
        catch (Exception ex)
        {
            Log.Info("Game list cache load failed: " + ex.Message);
        }

        return LoadBuiltin();
    }

    public static GameListData LoadBuiltin()
    {
        using var stream = typeof(GameListService).Assembly.GetManifestResourceStream("gamelist.min.json");
        if (stream == null) return new GameListData();
        return JsonSerializer.Deserialize<GameListData>(stream, Json.Options) ?? new GameListData();
    }

    public async Task<GameListData> DownloadAsync(CancellationToken ct = default)
    {
        using var response = await Http.GetAsync(DetectableUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Unexpected response from Discord.");

        var games = new List<Game>();
        foreach (var element in doc.RootElement.EnumerateArray())
        {
            if (!element.TryGetProperty("id", out var idProp) || !element.TryGetProperty("name", out var nameProp))
                continue;

            var id = idProp.GetString() ?? "";
            var name = (nameProp.GetString() ?? "").Trim();
            if (id.Length == 0 || name.Length == 0 || !id.All(char.IsDigit)) continue;

            if (!element.TryGetProperty("executables", out var exes) || exes.ValueKind != JsonValueKind.Array)
                continue;

            var list = new List<string>();
            foreach (var exe in exes.EnumerateArray())
            {
                if (!exe.TryGetProperty("os", out var os) || os.GetString() != "win32") continue;
                if (exe.TryGetProperty("is_launcher", out var launcher) && launcher.ValueKind == JsonValueKind.True) continue;
                if (!exe.TryGetProperty("name", out var exeName)) continue;

                var n = (exeName.GetString() ?? "").TrimStart('>').Trim();
                if (ValidExe(n) && !list.Contains(n)) list.Add(n);
            }

            if (list.Count > 0) games.Add(new Game { Id = id, Name = name, Exes = list });
        }

        if (games.Count < 100) throw new InvalidDataException("The downloaded list looks incomplete.");

        games.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        var result = new GameListData { UpdatedUtc = DateTime.UtcNow, Source = "discord", Games = games };
        SaveCache(result);
        return result;
    }

    public void SaveCache(GameListData data)
    {
        try
        {
            var tmp = AppPaths.GameListFile + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(data, Json.Options));
            File.Move(tmp, AppPaths.GameListFile, true);
        }
        catch (Exception ex)
        {
            Log.Info("Game list cache save failed: " + ex.Message);
        }
    }

    public void ClearCache()
    {
        try
        {
            if (File.Exists(AppPaths.GameListFile)) File.Delete(AppPaths.GameListFile);
        }
        catch (Exception ex)
        {
            Log.Info("Game list cache delete failed: " + ex.Message);
        }
    }

    /// <summary>Only plain relative .exe paths are accepted (no drive letters, no "..", no odd characters).</summary>
    public static bool ValidExe(string name)
    {
        if (name.Length == 0 || name.Length > 200) return false;
        if (!name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
        if (name.StartsWith('/') || name.StartsWith('.')) return false;

        foreach (var ch in name)
        {
            if (ch < 32 || "<>:\"|?*\\".IndexOf(ch) >= 0) return false;
        }

        foreach (var segment in name.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == "..") return false;
            if (segment.EndsWith('.') || segment.EndsWith(' ')) return false;
        }

        return true;
    }
}
