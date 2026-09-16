using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;

namespace FPSBooster.App.Services;

/// <summary>Mesure les FPS réels d'un jeu via PresentMon (Intel, open-source).
/// Nécessite PresentMon.exe dans %LocalAppData%\FPSBooster\tools\ (téléchargeable sur GitHub GameTechDev/PresentMon).</summary>
public static partial class FpsMeasureService
{
    public static string ToolDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", "tools");

    public static string ToolPath => Path.Combine(ToolDir, "PresentMon.exe");

    public static async Task<(bool Success, string Message)> MeasureAsync(int seconds = 15)
    {
        var (ok, fps, low1, frames, target, error) = await TryMeasureAverageFpsAsync(seconds);
        if (!ok) return (false, error);
        string msg = $"{target} : {fps:0} FPS moy / {low1:0} (1% low) sur {frames} frames";
        Logger.Info("FPS measure: " + msg);
        return (true, msg);
    }

    /// <summary>Mesure brute réutilisable (avant/après tweak) : FPS moyens + 1% low + nb frames.</summary>
    public static async Task<(bool Ok, double Fps, double Low1, int Frames, string Target, string Error)> TryMeasureAverageFpsAsync(int seconds = 15)
    {
        try
        {
            var games = ProcessService.DetectRunningGames();
            if (games.Length == 0)
                return (false, 0, 0, 0, "", "Lance d'abord ton jeu (aucun process connu détecté)");

            if (!File.Exists(ToolPath))
            {
                Directory.CreateDirectory(ToolDir);
                return (false, 0, 0, 0, "", $"PresentMon manquant : place PresentMon.exe dans {ToolDir} (github.com/GameTechDev/PresentMon)");
            }

            string target = games[0];
            string csv = Path.Combine(Path.GetTempPath(), "fpsbooster-presentmon.csv");
            try { if (File.Exists(csv)) File.Delete(csv); }
            catch (Exception ex) { Logger.Warn($"FPS measure nettoyage CSV: {ex.Message}"); }

            Logger.Info($"FPS measure: {target} pendant {seconds}s");
            var (code, _, _) = await CmdHelper.RunAsync(ToolPath,
                $"-process_name {target} -timed {seconds} -output_file \"{csv}\"", (seconds + 25) * 1000);

            if (!File.Exists(csv))
                return (false, 0, 0, 0, target, $"PresentMon exit={code}, pas de données (jeu en plein écran exclusif requis)");

            var values = new List<double>();
            string[] allLines = File.ReadAllLines(csv);
            if (allLines.Length < 12)
                return (false, 0, 0, 0, target, $"Pas assez de frames capturées — jeu en plein écran ?");
            string[] header = allLines[0].Split(',');
            int idx = Array.FindIndex(header, h => h.Trim().Equals("MsBetweenPresents", StringComparison.OrdinalIgnoreCase));
            if (idx < 0)
                return (false, 0, 0, 0, target, "CSV PresentMon inattendu (colonne MsBetweenPresents absente)");
            foreach (var line in allLines.Skip(1))
            {
                var cols = line.Split(',');
                if (idx >= cols.Length) continue;
                if (double.TryParse(cols[idx].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double ms) && ms > 0)
                    values.Add(ms);
            }
            try { File.Delete(csv); }
            catch (Exception ex) { Logger.Warn($"FPS measure suppression CSV: {ex.Message}"); }
            if (values.Count < 10)
                return (false, 0, 0, 0, target, $"Pas assez de frames capturées ({values.Count}) — jeu en plein écran ?");

            values.Sort();
            double avg = values.Average();
            double p1 = values[(int)(values.Count * 0.99)]; // ~1% low en ms
            FpsGainService.SaveLastFps(1000.0 / avg, 1000.0 / p1, values.Count, target);
            return (true, 1000.0 / avg, 1000.0 / p1, values.Count, target, "");
        }
        catch (Exception ex)
        {
            Logger.Error("FPS measure", ex);
            return (false, 0, 0, 0, "", "Mesure échouée : " + ex.Message);
        }
    }

}
