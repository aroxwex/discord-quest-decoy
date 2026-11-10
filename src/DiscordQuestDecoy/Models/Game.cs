namespace DiscordQuestDecoy.Models;

public sealed class Game
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public List<string> Exes { get; set; } = new();
    public string? SelectedExe { get; set; }
    public bool Custom { get; set; }
}

public sealed class GameListData
{
    public DateTime UpdatedUtc { get; set; }
    public string Source { get; set; } = "builtin";
    public List<Game> Games { get; set; } = new();
}

public sealed class AppSettings
{
    public string Language { get; set; } = "en";
    public bool StopOnExit { get; set; } = true;
    public bool AutoUpdateList { get; set; } = true;
    public List<Game> MyGames { get; set; } = new();
}
