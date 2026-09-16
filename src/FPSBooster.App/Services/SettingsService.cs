using System.IO;

namespace FPSBooster.App.Services;

/// <summary>Réglages persistés (key=value). Compatible ancien settings.txt (langue seule).</summary>
public static class SettingsService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", "settings.txt");

    private static readonly Dictionary<string, string> Cache = Load();

    private static Dictionary<string, string> Load()
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            if (!File.Exists(FilePath)) return d;
            foreach (var line in File.ReadAllLines(FilePath))
            {
                string t = line.Trim();
                if (t.Length == 0 || t.StartsWith('#')) continue;
                int eq = t.IndexOf('=');
                if (eq > 0) d[t[..eq].Trim()] = t[(eq + 1)..].Trim();
                else if (!d.ContainsKey("lang")) d["lang"] = t.ToLowerInvariant(); // legacy
            }
        }
        catch (Exception ex) { Logger.Error("Settings load", ex); }
        return d;
    }

    public static bool GetDarkMode() => Get("darkmode", "true").Equals("true", StringComparison.OrdinalIgnoreCase);

    public static void SetDarkMode(bool dark) => Set("darkmode", dark.ToString());

    public static string Get(string key, string def = "") =>
        Cache.TryGetValue(key, out string? v) ? v : def;

    public static void Set(string key, string value)
    {
        Cache[key] = value;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllLines(FilePath, Cache.Select(kv => $"{kv.Key}={kv.Value}"));
        }
        catch (Exception ex) { Logger.Error("Settings save", ex); }
    }
}
