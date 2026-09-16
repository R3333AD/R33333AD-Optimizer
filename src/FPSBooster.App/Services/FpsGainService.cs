using System.IO;
using System.Text.Json;
using FPSBooster.App.Models;

namespace FPSBooster.App.Services;

/// <summary>Gains FPS réels mesurés par tweak (avant/après via PresentMon).
/// Persistés dans fpsgains.json : { ActionId: {avant, après, gain%, date} }.</summary>
public static class FpsGainService
{
    public record FpsGainEntry(double Before, double After, double GainPct, string Date);

    public record LastFps(double Fps, double Low1, int Frames, string Target, string Date);

    private static readonly string LastFpsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", "lastfps.json");

    private static LastFps? _lastFpsCache;
    private static bool _lastFpsLoaded;

    /// <summary>Mémorise la dernière mesure (affichée dans l'overlay).</summary>
    public static void SaveLastFps(double fps, double low1, int frames, string target)
    {
        try
        {
            lock (Lock)
            {
                _lastFpsCache = new LastFps(fps, low1, frames, target, DateTime.Now.ToString("o"));
                Directory.CreateDirectory(Path.GetDirectoryName(LastFpsPath)!);
                File.WriteAllText(LastFpsPath, JsonSerializer.Serialize(_lastFpsCache));
                _lastFpsLoaded = true;
            }
        }
        catch (Exception ex) { Logger.Warn($"LastFPS save: {ex.Message}"); }
    }

    public static string LastFpsText()
    {
        try
        {
            lock (Lock)
            {
                if (!_lastFpsLoaded)
                {
                    _lastFpsLoaded = true;
                    if (File.Exists(LastFpsPath))
                        _lastFpsCache = JsonSerializer.Deserialize<LastFps>(File.ReadAllText(LastFpsPath));
                }
                return _lastFpsCache == null ? "—" : $"{_lastFpsCache.Fps:0}";
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"LastFPS load: {ex.Message}");
            return "—";
        }
    }

    public static string LastFpsTip()
    {
        var c = _lastFpsCache;
        return c == null ? "Aucune mesure FPS" : $"{c.Target} • {c.Fps:0} FPS moy ({c.Date[..16].Replace('T', ' ')})";
    }

    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", "fpsgains.json");

    private static readonly object Lock = new();

    public static double ComputeGainPct(double before, double after) =>
        before <= 0 ? 0 : (after - before) / before * 100.0;

    public static string FormatMeasured(double gain) =>
        $"mesuré {(gain >= 0 ? "+" : "")}{gain:0.0} %";

    public static Dictionary<string, FpsGainEntry> Load() =>
        LoadHistory().ToDictionary(
            kv => kv.Key,
            kv => new FpsGainEntry(
                kv.Value[^1].Before, kv.Value[^1].After,
                kv.Value.Average(x => x.GainPct), kv.Value[^1].Date));

    /// <summary>Historique complet (5 dernières mesures max par tweak), avec migration de l'ancien format.</summary>
    public static Dictionary<string, List<FpsGainEntry>> LoadHistory()
    {
        try
        {
            lock (Lock)
            {
                if (!File.Exists(FilePath)) return new();
                string json = File.ReadAllText(FilePath);
                try
                {
                    var d = JsonSerializer.Deserialize<Dictionary<string, List<FpsGainEntry>>>(json);
                    if (d != null) return d;
                }
                catch { /* pas le nouveau format, tente l'ancien */ }
                var old = JsonSerializer.Deserialize<Dictionary<string, FpsGainEntry>>(json);
                if (old == null) return new();
                Logger.Info("Gains: migration ancien format -> historique");
                return old.ToDictionary(kv => kv.Key, kv => new List<FpsGainEntry> { kv.Value });
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"Gains load: {ex.Message}");
            return new();
        }
    }

    public static bool TryGet(string actionId, out FpsGainEntry entry) =>
        Load().TryGetValue(actionId, out entry!);

    public static void SaveGain(string actionId, double before, double after, double gain)
    {
        try
        {
            lock (Lock)
            {
                var d = LoadHistory();
                if (!d.TryGetValue(actionId, out var list))
                {
                    list = new List<FpsGainEntry>();
                    d[actionId] = list;
                }
                list.Add(new FpsGainEntry(before, after, gain, DateTime.Now.ToString("o")));
                while (list.Count > 5) list.RemoveAt(0); // 5 dernières mesures max
                Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(d));
            }
            Logger.Info($"Gain mesuré {actionId}: {before:0} -> {after:0} FPS ({FormatMeasured(gain)})");
        }
        catch (Exception ex) { Logger.Error("Gains save", ex); }
    }

    /// <summary>Applique un gain mesuré stocké sur l'item (écrase l'estimation).</summary>
    public static void ApplyMeasured(ToolItem item)
    {
        try
        {
            if (Load().TryGetValue(item.ActionId, out var e))
            {
                item.GainText = FormatMeasured(e.GainPct);
                item.GainSort = e.GainPct;
            }
        }
        catch (Exception ex) { Logger.Warn($"ApplyMeasured {item.ActionId}: {ex.Message}"); }
    }

    /// <summary>Mesure avant → applique le tweak → mesure après → calcule et stocke le gain.</summary>
    public static async Task<(bool Success, double GainPct, string Message)> MeasureGainAsync(
        string actionId, Func<Task<TweakResult>> apply, IProgress<double>? progress = null, int seconds = 10)
    {
        try
        {
            progress?.Report(5);
            var (okB, fpsB, _, _, target, errB) = await FpsMeasureService.TryMeasureAverageFpsAsync(seconds);
            if (!okB) return (false, 0, errB);
            progress?.Report(40);

            TweakResult res;
            try { res = await apply(); }
            catch (Exception ex)
            {
                Logger.Error($"Gain {actionId} apply", ex);
                return (false, 0, ex.Message);
            }
            if (!res.Success) return (false, 0, res.Message);
            progress?.Report(60);

            var (okA, fpsA, _, _, _, errA) = await FpsMeasureService.TryMeasureAverageFpsAsync(seconds);
            if (!okA) return (false, 0, errA + " (tweak déjà appliqué)");
            progress?.Report(95);

            double gain = ComputeGainPct(fpsB, fpsA);
            SaveGain(actionId, fpsB, fpsA, gain);
            return (true, gain, $"{target} : {fpsB:0} → {fpsA:0} FPS ({FormatMeasured(gain)})");
        }
        catch (Exception ex)
        {
            Logger.Error($"Gain {actionId}", ex);
            return (false, 0, "Mesure échouée : " + ex.Message);
        }
    }
}
