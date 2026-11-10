using DiscordQuestDecoy.Models;

namespace DiscordQuestDecoy.ViewModels;

/// <summary>A row in the search results dropdown.</summary>
public sealed class SearchItem : ObservableObject
{
    bool _isAdded;

    public SearchItem(Game game) => Game = game;

    public Game Game { get; }
    public string Name => Game.Custom ? "[custom] " + Game.Name : Game.Name;
    public string ExeText => Game.Exes.FirstOrDefault() ?? "";

    public bool IsAdded
    {
        get => _isAdded;
        set
        {
            if (Set(ref _isAdded, value)) OnPropertyChanged(nameof(CanAdd));
        }
    }

    public bool CanAdd => !_isAdded;
}
