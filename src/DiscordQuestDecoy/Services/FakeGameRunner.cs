using System.Diagnostics;
using System.Runtime.InteropServices;
using DiscordQuestDecoy.Models;

namespace DiscordQuestDecoy.Services;

/// <summary>
/// Starts and stops the stand-in programs. A stand-in is this very app, hard-linked (or copied)
/// under the game's executable name and launched with "--sleeper". Everything stays inside data\games.
/// </summary>
public sealed class FakeGameRunner
{
    readonly Dictionary<string, Process> _procs = new();
    readonly object _lock = new();

    public event Action<string>? GameExited;

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    static extern bool CreateHardLink(string lpFileName, string lpExistingFileName, IntPtr lpSecurityAttributes);

    public bool IsRunning(string gameId)
    {
        lock (_lock)
        {
            return _procs.TryGetValue(gameId, out var p) && !HasExited(p);
        }
    }

    static bool HasExited(Process p)
    {
        try { return p.HasExited; }
        catch { return true; }
    }

    public void Start(Game game, string exe, string message)
    {
        if (IsRunning(game.Id)) return;

        if (game.Id.Length == 0 || !game.Id.All(char.IsDigit))
            throw new InvalidOperationException("Invalid game id.");
        if (!GameListService.ValidExe(exe))
            throw new InvalidOperationException("Invalid executable name.");

        var self = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the app executable.");

        var root = Path.GetFullPath(AppPaths.GamesDir);
        var full = Path.GetFullPath(Path.Combine(root, game.Id, exe.Replace('/', '\\')));
        if (!full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Path escapes the games folder.");

        var dir = Path.GetDirectoryName(full)!;
        Directory.CreateDirectory(dir);
        EnsureStandIn(self, full);

        var psi = new ProcessStartInfo(full)
        {
            UseShellExecute = false,
            WorkingDirectory = dir,
        };
        psi.ArgumentList.Add("--sleeper");
        psi.ArgumentList.Add(game.Name);
        psi.ArgumentList.Add("--text");
        psi.ArgumentList.Add(message);

        var process = Process.Start(psi) ?? throw new InvalidOperationException("The process did not start.");
        var id = game.Id;
        process.EnableRaisingEvents = true;
        process.Exited += (_, _) => OnExited(id, process);

        lock (_lock)
        {
            _procs[id] = process;
        }

        Log.Info($"Started {game.Name} as {full}");
    }

    static void EnsureStandIn(string self, string target)
    {
        if (File.Exists(target))
        {
            var a = new FileInfo(self);
            var b = new FileInfo(target);
            if (a.Length == b.Length && a.LastWriteTimeUtc == b.LastWriteTimeUtc) return;

            try { File.Delete(target); }
            catch { return; } // in use: keep the old stand-in
        }

        if (CreateHardLink(target, self, IntPtr.Zero)) return;
        File.Copy(self, target, true);
    }

    void OnExited(string id, Process process)
    {
        lock (_lock)
        {
            if (_procs.TryGetValue(id, out var current) && ReferenceEquals(current, process))
                _procs.Remove(id);
        }

        GameExited?.Invoke(id);
    }

    public void Stop(string gameId)
    {
        Process? p;
        lock (_lock)
        {
            _procs.TryGetValue(gameId, out p);
            _procs.Remove(gameId);
        }

        if (p == null) return;
        try
        {
            if (!p.HasExited) p.Kill();
        }
        catch (Exception ex)
        {
            Log.Info("Stop failed: " + ex.Message);
        }
    }

    public void StopAll()
    {
        List<string> ids;
        lock (_lock)
        {
            ids = _procs.Keys.ToList();
        }

        foreach (var id in ids) Stop(id);
    }

    /// <summary>Re-attaches to stand-ins left running by a previous session (for example if "stop on exit" was off).</summary>
    public List<string> AdoptExisting()
    {
        var found = new List<string>();
        var root = Path.GetFullPath(AppPaths.GamesDir) + Path.DirectorySeparatorChar;

        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var path = p.MainModule?.FileName;
                if (path == null || !path.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;

                var id = path.Substring(root.Length).Split('\\')[0];
                if (id.Length == 0 || !id.All(char.IsDigit)) continue;

                var proc = p;
                proc.EnableRaisingEvents = true;
                proc.Exited += (_, _) => OnExited(id, proc);
                lock (_lock)
                {
                    _procs[id] = proc;
                }
                found.Add(id);
            }
            catch
            {
                // inaccessible process: ignore
            }
        }

        return found;
    }
}
