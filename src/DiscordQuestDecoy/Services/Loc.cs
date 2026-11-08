using System.ComponentModel;
using System.Text.Json;

namespace DiscordQuestDecoy.Services;

public sealed record LangInfo(string Code, string Name);

/// <summary>
/// Loads translations from embedded resources (en, tr) and from the "languages" folder.
/// Files in the folder override or extend the built-in ones. English is always the fallback.
/// </summary>
public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new Loc();

    readonly Dictionary<string, Dictionary<string, string>> _langs = new(StringComparer.OrdinalIgnoreCase);
    Dictionary<string, string> _en = new();
    Dictionary<string, string> _cur = new();

    public event PropertyChangedEventHandler? PropertyChanged;
    public string Code { get; private set; } = "en";

    Loc()
    {
        LoadEmbedded();
        LoadFolder();
        _en = _langs.TryGetValue("en", out var en) ? en : new Dictionary<string, string>();
        _cur = _en;
    }

    public string this[string key]
    {
        get
        {
            if (_cur.TryGetValue(key, out var v)) return v;
            if (_en.TryGetValue(key, out var d)) return d;
            return key;
        }
    }

    public string F(string key, params object[] args)
    {
        var template = this[key];
        try
        {
            return string.Format(template, args);
        }
        catch (FormatException)
        {
            return template;
        }
    }

    public IReadOnlyList<LangInfo> Available =>
        _langs.Select(kv => new LangInfo(kv.Key, kv.Value.TryGetValue("_name", out var n) ? n : kv.Key))
              .OrderBy(l => l.Code.Equals("en", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
              .ThenBy(l => l.Name, StringComparer.CurrentCultureIgnoreCase)
              .ToList();

    public void SetLanguage(string code)
    {
        if (!_langs.TryGetValue(code, out var dict))
        {
            code = "en";
            dict = _en;
        }

        Code = code;
        _cur = dict;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Code)));
    }

    void LoadEmbedded()
    {
        var asm = typeof(Loc).Assembly;
        foreach (var res in asm.GetManifestResourceNames())
        {
            if (!res.StartsWith("lang.", StringComparison.Ordinal) || !res.EndsWith(".json", StringComparison.Ordinal))
                continue;

            var code = res.Substring(5, res.Length - 10);
            using var stream = asm.GetManifestResourceStream(res);
            if (stream == null) continue;
            var dict = Parse(stream);
            if (dict != null) Merge(code, dict);
        }
    }

    void LoadFolder()
    {
        try
        {
            if (!Directory.Exists(AppPaths.LanguagesDir)) return;
            foreach (var file in Directory.GetFiles(AppPaths.LanguagesDir, "*.json"))
            {
                try
                {
                    var code = Path.GetFileNameWithoutExtension(file);
                    using var stream = File.OpenRead(file);
                    var dict = Parse(stream);
                    if (dict != null) Merge(code, dict);
                }
                catch (Exception ex)
                {
                    Log.Info("Language file skipped (" + file + "): " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Info("Language folder error: " + ex.Message);
        }
    }

    void Merge(string code, Dictionary<string, string> dict)
    {
        if (_langs.TryGetValue(code, out var existing))
        {
            foreach (var kv in dict) existing[kv.Key] = kv.Value;
        }
        else
        {
            _langs[code] = dict;
        }
    }

    static Dictionary<string, string>? Parse(Stream stream)
    {
        try
        {
            using var doc = JsonDocument.Parse(stream, new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            var dict = new Dictionary<string, string>();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Value.ValueKind == JsonValueKind.String)
                    dict[prop.Name] = prop.Value.GetString() ?? "";
            }
            return dict;
        }
        catch
        {
            return null;
        }
    }
}
