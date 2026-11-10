using DiscordQuestDecoy.Models;

namespace DiscordQuestDecoy.ViewModels;

/// <summary>A game in the "My games" list.</summary>
public sealed class GameItem : ObservableObject
{
    bool _isRunning;

    public GameItem(Game game)
    {
        Game = game;
        if (Game.SelectedExe == null || !Game.Exes.Contains(Game.SelectedExe))
            Game.SelectedExe = Game.Exes.FirstOrDefault();
    }

    public Game Game { get; }
    public string Id => Game.Id;
    public string Name => Game.Name;
    public string DisplayName => Game.Custom ? "[custom] " + Game.Name : Game.Name;
    public IReadOnlyList<string> Exes => Game.Exes;

    /// <summary>Called when something that should be saved changes.</summary>
    public Action? Changed { get; set; }

    public string? SelectedExe
    {
        get => Game.SelectedExe;
        set
        {
            if (value == null || value == Game.SelectedExe) return;
            Game.SelectedExe = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ExeLabel));
            Changed?.Invoke();
        }
    }

    public string ExeLabel => Game.SelectedExe ?? "";

    public bool IsRunning
    {
        get => _isRunning;
        set
        {
            if (Set(ref _isRunning, value)) OnPropertyChanged(nameof(CanEdit));
        }
    }

    public bool CanEdit => !_isRunning;

    /// <summary>Applies a refreshed executable list from Discord (skipped while the game is running).</summary>
    public void UpdateExes(List<string> exes)
    {
        if (_isRunning || exes.Count == 0 || Game.Exes.SequenceEqual(exes)) return;

        Game.Exes = new List<string>(exes);
        if (Game.SelectedExe == null || !Game.Exes.Contains(Game.SelectedExe))
            Game.SelectedExe = Game.Exes[0];

        OnPropertyChanged(nameof(Exes));
        OnPropertyChanged(nameof(SelectedExe));
        OnPropertyChanged(nameof(ExeLabel));
        Changed?.Invoke();
    }
}
