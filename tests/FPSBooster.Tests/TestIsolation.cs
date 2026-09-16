using System.IO;
using System.Reflection;
using FPSBooster.App.Services;

namespace FPSBooster.Tests;

/// <summary>Sauvegarde/restaure le vrai settings.txt + le cache statique,
/// pour que les tests ne polluent jamais la config de l'utilisateur.</summary>
internal static class TestIsolation
{
    private static byte[]? _backup;
    private static bool _taken;
    private static readonly Dictionary<string, byte[]?> _extraBackups = new();

    private static string AppFile(string name) => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", name);

    private static string SettingsPath => AppFile("settings.txt");

    // Fichiers disque touchés par les tests (gains, benchmark, backup reg, journal, dernier FPS)
    private static string[] ExtraFiles() => new[]
    {
        AppFile("fpsgains.json"), AppFile("bench.json"), AppFile("bench-history.json"),
        AppFile($"regbackup-{DateTime.Now:yyyyMMdd}.reg"),
        AppFile("journal.jsonl"), AppFile("lastfps.json"),
    };

    public static void BackupSettings()
    {
        if (_taken) return;
        _taken = true;
        try { _backup = File.Exists(SettingsPath) ? File.ReadAllBytes(SettingsPath) : null; }
        catch { _backup = null; }
        _extraBackups.Clear();
        foreach (var f in ExtraFiles())
        {
            try { _extraBackups[f] = File.Exists(f) ? File.ReadAllBytes(f) : null; }
            catch { _extraBackups[f] = null; }
        }
    }

    public static void RestoreSettings()
    {
        if (!_taken) return;
        _taken = false;
        try
        {
            if (_backup == null)
            {
                if (File.Exists(SettingsPath)) File.Delete(SettingsPath);
            }
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
                File.WriteAllBytes(SettingsPath, _backup);
            }
        }
        catch { }
        try
        {
            // Recharge le cache statique depuis le fichier restauré
            var t = typeof(SettingsService);
            var cache = (Dictionary<string, string>)t
                .GetField("Cache", BindingFlags.Static | BindingFlags.NonPublic)!
                .GetValue(null)!;
            var fresh = (Dictionary<string, string>)t
                .GetMethod("Load", BindingFlags.Static | BindingFlags.NonPublic)!
                .Invoke(null, null)!;
            cache.Clear();
            foreach (var kv in fresh) cache[kv.Key] = kv.Value;
        }
        catch { }
        try
        {
            foreach (var (f, data) in _extraBackups)
            {
                if (data == null) { if (File.Exists(f)) File.Delete(f); }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(f)!);
                    File.WriteAllBytes(f, data);
                }
            }
        }
        catch { }
        _backup = null;
        _extraBackups.Clear();
    }
}
