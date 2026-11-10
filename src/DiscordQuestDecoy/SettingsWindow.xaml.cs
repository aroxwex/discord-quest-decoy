using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using DiscordQuestDecoy.Services;

namespace DiscordQuestDecoy;

public partial class SettingsWindow : Window, INotifyPropertyChanged
{
    readonly Func<Task<(bool Ok, string Message)>> _refresh;
    readonly Func<string> _listInfo;
    readonly Action _clearCache;
    bool _init = true;
    string _listStatus = "";

    public event PropertyChangedEventHandler? PropertyChanged;

    void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public Loc L => Loc.Instance;
    public bool HasRepo => AppInfo.RepoConfigured;
    public string VersionText => L.F("settings_version", AppInfo.PlainVersion);
    public string ListInfo => _listInfo();

    public string ListStatus
    {
        get => _listStatus;
        private set
        {
            _listStatus = value;
            OnPropertyChanged();
        }
    }

    public bool StopOnExit
    {
        get => SettingsStore.Current.StopOnExit;
        set
        {
            SettingsStore.Current.StopOnExit = value;
            SettingsStore.Save();
        }
    }

    public bool AutoUpdateList
    {
        get => SettingsStore.Current.AutoUpdateList;
        set
        {
            SettingsStore.Current.AutoUpdateList = value;
            SettingsStore.Save();
        }
    }

    public SettingsWindow(Func<Task<(bool Ok, string Message)>> refresh, Func<string> listInfo, Action clearCache)
    {
        _refresh = refresh;
        _listInfo = listInfo;
        _clearCache = clearCache;

        InitializeComponent();
        DataContext = this;

        LangList.ItemsSource = Loc.Instance.Available;
        LangList.SelectedValue = Loc.Instance.Code;
        _init = false;

        SourceInitialized += (_, _) => Win.DarkTitleBar(this);
        Loc.Instance.PropertyChanged += OnLocChanged;
        Closed += (_, _) => Loc.Instance.PropertyChanged -= OnLocChanged;
    }

    void OnLocChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != "Item[]") return;
        OnPropertyChanged(nameof(VersionText));
        OnPropertyChanged(nameof(ListInfo));
    }

    void Lang_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_init || LangList.SelectedValue is not string code) return;
        SettingsStore.Current.Language = code;
        SettingsStore.Save();
        Loc.Instance.SetLanguage(code);
    }

    async void UpdateNow_Click(object sender, RoutedEventArgs e)
    {
        ListStatus = L["settings_updating"];
        var (_, message) = await _refresh();
        ListStatus = message;
        OnPropertyChanged(nameof(ListInfo));
    }

    void ClearCache_Click(object sender, RoutedEventArgs e)
    {
        _clearCache();
        ListStatus = L["settings_cachecleared"];
        OnPropertyChanged(nameof(ListInfo));
    }

    void OpenData_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppPaths.DataDir);
        Win.Open(AppPaths.DataDir);
    }

    void OpenLang_Click(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(AppPaths.LanguagesDir);
        Win.Open(AppPaths.LanguagesDir);
    }

    void OpenExtra_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!File.Exists(ExtraGamesService.FilePath))
                File.WriteAllText(ExtraGamesService.FilePath, ExtraGamesService.Template);
            Win.Open(ExtraGamesService.FilePath);
        }
        catch (Exception ex)
        {
            Log.Info("Could not open the extra games file: " + ex.Message);
        }
    }

    void OpenLog_Click(object sender, RoutedEventArgs e)
    {
        if (!File.Exists(AppPaths.LogFile)) File.WriteAllText(AppPaths.LogFile, "");
        Win.Open(AppPaths.LogFile);
    }

    void GitHub_Click(object sender, RoutedEventArgs e) => Win.Open(AppInfo.RepoUrl);
}
