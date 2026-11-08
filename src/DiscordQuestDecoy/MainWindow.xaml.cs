using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DiscordQuestDecoy.Models;
using DiscordQuestDecoy.Services;
using DiscordQuestDecoy.ViewModels;

namespace DiscordQuestDecoy;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    readonly GameListService _lists = new();
    readonly FakeGameRunner _runner = new();

    GameListData _data = new();
    List<(Game Game, string Lower)> _index = new();
    string _searchText = "";
    bool _showResults;
    GameItem? _selected;

    public ObservableCollection<GameItem> MyGames { get; } = new();
    public ObservableCollection<SearchItem> SearchResults { get; } = new();

    public Loc L => Loc.Instance;
    public string VersionLabel => AppInfo.VersionLabel;

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        SourceInitialized += (_, _) => Win.DarkTitleBar(this);
        PreviewMouseDown += OnPreviewMouseDown;
        Loc.Instance.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == "Item[]") RefreshTexts();
        };
        MyGames.CollectionChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(HasNoGames));
            OnPropertyChanged(nameof(MyGamesCountText));
        };

        _data = _lists.LoadLocal();
        RebuildIndex();

        foreach (var saved in SettingsStore.Current.MyGames)
        {
            if (!IsValidSaved(saved)) continue;
            MyGames.Add(Wrap(saved));
        }

        _runner.GameExited += id => Dispatcher.BeginInvoke(new Action(() => OnGameExited(id)));
        foreach (var id in _runner.AdoptExisting())
        {
            var item = MyGames.FirstOrDefault(m => m.Id == id);
            if (item != null) item.IsRunning = true;
        }

        Selected = MyGames.FirstOrDefault();
        RefreshTexts();

        if (SettingsStore.Current.AutoUpdateList)
            _ = RefreshListAsync();
    }

    // ---------- Bindable state ----------

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (_searchText == value) return;
            _searchText = value;
            OnPropertyChanged();
            RunSearch();
        }
    }

    public bool ShowResults
    {
        get => _showResults;
        private set
        {
            if (_showResults == value) return;
            _showResults = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(NoResults));
        }
    }

    public bool NoResults => ShowResults && SearchResults.Count == 0;

    public GameItem? Selected
    {
        get => _selected;
        set
        {
            if (ReferenceEquals(_selected, value)) return;
            _selected = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelected));
            OnPropertyChanged(nameof(ShowHint));
            OnPropertyChanged(nameof(SelectedSubtitle));
        }
    }

    public bool HasSelected => Selected != null;
    public bool ShowHint => Selected == null;
    public bool HasNoGames => MyGames.Count == 0;
    public int RunningCount => MyGames.Count(g => g.IsRunning);
    public bool HasRunning => RunningCount > 0;

    public string MyGamesCountText => L.F("mygames_count", MyGames.Count);
    public string SelectedSubtitle => Selected == null ? "" : L.F("sel_exes", Selected.Exes.Count);
    public string StopAllText => L.F("btn_stopall", RunningCount);
    public string SearchPlaceholder => L.F("search_placeholder", _data.Games.Count.ToString("N0"));
    public string FooterText => L.F("footer_list", SourceName, UpdatedText, _data.Games.Count.ToString("N0"));

    string SourceName => _data.Source == "discord" ? L["source_discord"] : L["source_builtin"];
    string UpdatedText => _data.UpdatedUtc == default ? "-" : _data.UpdatedUtc.ToLocalTime().ToString("g");
    string ListInfo => L.F("settings_listinfo", SourceName, UpdatedText, _data.Games.Count.ToString("N0"));

    void RefreshTexts()
    {
        OnPropertyChanged(nameof(MyGamesCountText));
        OnPropertyChanged(nameof(SelectedSubtitle));
        OnPropertyChanged(nameof(StopAllText));
        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(FooterText));
    }

    // ---------- Game list ----------

    static bool IsValidSaved(Game g) =>
        g.Id.Length > 0 && g.Id.All(char.IsDigit) && g.Exes.Count > 0 && g.Exes.All(GameListService.ValidExe);

    void RebuildIndex() =>
        _index = _data.Games.Select(g => (g, g.Name.ToLowerInvariant())).ToList();

    void ApplyList(GameListData data)
    {
        _data = data;
        RebuildIndex();

        var map = new Dictionary<string, Game>();
        foreach (var g in data.Games) map.TryAdd(g.Id, g);
        foreach (var item in MyGames)
        {
            if (map.TryGetValue(item.Id, out var fresh)) item.UpdateExes(fresh.Exes);
        }

        OnPropertyChanged(nameof(SearchPlaceholder));
        OnPropertyChanged(nameof(FooterText));
        OnPropertyChanged(nameof(SelectedSubtitle));
        if (ShowResults) RunSearch();
    }

    public async Task<(bool Ok, string Message)> RefreshListAsync()
    {
        try
        {
            var data = await _lists.DownloadAsync();
            ApplyList(data);
            return (true, L.F("settings_updated", data.Games.Count.ToString("N0")));
        }
        catch (Exception ex)
        {
            Log.Info("Game list download failed: " + ex.Message);
            return (false, L["settings_updatefailed"]);
        }
    }

    void ClearCache()
    {
        _lists.ClearCache();
        ApplyList(GameListService.LoadBuiltin());
    }

    // ---------- Search ----------

    void RunSearch()
    {
        SearchResults.Clear();
        var q = _searchText.Trim().ToLowerInvariant();
        if (q.Length < 2)
        {
            ShowResults = false;
            OnPropertyChanged(nameof(NoResults));
            return;
        }

        var starts = new List<Game>();
        var inside = new List<Game>();
        foreach (var (game, lower) in _index)
        {
            var p = lower.IndexOf(q, StringComparison.Ordinal);
            if (p == 0) starts.Add(game);
            else if (p > 0) inside.Add(game);
        }

        foreach (var g in starts.Concat(inside).Take(50))
            SearchResults.Add(new SearchItem(g) { IsAdded = MyGames.Any(m => m.Id == g.Id) });

        ShowResults = true;
        OnPropertyChanged(nameof(NoResults));
    }

    void SearchBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_searchText.Trim().Length >= 2) ShowResults = true;
    }

    void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            ShowResults = false;
            e.Handled = true;
        }
    }

    void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!ShowResults) return;
        if (e.OriginalSource is DependencyObject d && !IsInside(d, ResultsPanel) && !IsInside(d, SearchBox))
            ShowResults = false;
    }

    static bool IsInside(DependencyObject? d, DependencyObject root)
    {
        while (d != null)
        {
            if (ReferenceEquals(d, root)) return true;
            d = d is Visual ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
        }
        return false;
    }

    // ---------- My games ----------

    GameItem Wrap(Game game)
    {
        var item = new GameItem(game) { Changed = SaveGames };
        item.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(GameItem.IsRunning)) OnRunningChanged();
        };
        return item;
    }

    void OnRunningChanged()
    {
        OnPropertyChanged(nameof(RunningCount));
        OnPropertyChanged(nameof(HasRunning));
        OnPropertyChanged(nameof(StopAllText));
    }

    void SaveGames()
    {
        SettingsStore.Current.MyGames = MyGames.Select(m => m.Game).ToList();
        SettingsStore.Save();
    }

    void AddGame(Game source)
    {
        if (MyGames.Any(m => m.Id == source.Id)) return;

        var copy = new Game
        {
            Id = source.Id,
            Name = source.Name,
            Exes = new List<string>(source.Exes),
            SelectedExe = source.Exes.FirstOrDefault(),
        };

        var item = Wrap(copy);
        MyGames.Add(item);
        Selected = item;
        SaveGames();
    }

    void AddResult_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: SearchItem item })
        {
            AddGame(item.Game);
            item.IsAdded = true;
        }
    }

    void RemoveRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: GameItem item }) RemoveGame(item);
    }

    void RemoveSelected_Click(object sender, RoutedEventArgs e)
    {
        if (Selected != null) RemoveGame(Selected);
    }

    void GamesList_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Delete && Selected != null)
        {
            RemoveGame(Selected);
            e.Handled = true;
        }
    }

    void RemoveGame(GameItem item)
    {
        if (item.IsRunning)
        {
            _runner.Stop(item.Id);
            item.IsRunning = false;
        }

        var index = MyGames.IndexOf(item);
        MyGames.Remove(item);
        Selected = MyGames.Count > 0 ? MyGames[Math.Min(Math.Max(index, 0), MyGames.Count - 1)] : null;

        DeleteGameFolder(item.Id);
        foreach (var r in SearchResults)
        {
            if (r.Game.Id == item.Id) r.IsAdded = false;
        }

        SaveGames();
    }

    static void DeleteGameFolder(string id)
    {
        if (id.Length == 0 || !id.All(char.IsDigit)) return;
        try
        {
            var dir = Path.Combine(AppPaths.GamesDir, id);
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
        catch (Exception ex)
        {
            Log.Info("Could not delete game folder: " + ex.Message);
        }
    }

    // ---------- Play / stop ----------

    void Play_Click(object sender, RoutedEventArgs e)
    {
        var item = Selected;
        if (item == null || item.IsRunning) return;

        var exe = item.SelectedExe ?? item.Game.Exes.FirstOrDefault();
        if (exe == null) return;

        try
        {
            _runner.Start(item.Game, exe, L["sleeper_text"]);
            item.IsRunning = true;
        }
        catch (Exception ex)
        {
            Log.Info("Start failed: " + ex);
            MessageBox.Show(this, L.F("err_start", ex.Message), AppInfo.Name, MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void Stop_Click(object sender, RoutedEventArgs e)
    {
        var item = Selected;
        if (item == null) return;
        _runner.Stop(item.Id);
        item.IsRunning = false;
    }

    void StopAll_Click(object sender, RoutedEventArgs e)
    {
        _runner.StopAll();
        foreach (var m in MyGames) m.IsRunning = false;
    }

    void OnGameExited(string id)
    {
        var item = MyGames.FirstOrDefault(m => m.Id == id);
        if (item != null) item.IsRunning = false;
    }

    // ---------- Window ----------

    void Settings_Click(object sender, RoutedEventArgs e)
    {
        var window = new SettingsWindow(RefreshListAsync, () => ListInfo, ClearCache) { Owner = this };
        window.ShowDialog();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (SettingsStore.Current.StopOnExit) _runner.StopAll();
        SaveGames();
        base.OnClosing(e);
    }
}
