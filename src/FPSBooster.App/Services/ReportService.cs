using System.IO;
using System.Text;
using FPSBooster.App.Models;

namespace FPSBooster.App.Services;

/// <summary>Rapport Markdown exportable (gains estimés + mesurés, benchmark, journal).</summary>
public static class ReportService
{
    /// <summary>Source des outils affichés (branchée par MainWindow). Repli : catalogue TweakLibrary.</summary>
    public static Func<IEnumerable<ToolItem>>? ToolSource;

    public static TweakResult Export()
    {
        try
        {
            var tools = ToolSource?.Invoke()
                ?? TweakLibrary.All.Select(d => new ToolItem
                {
                    ActionId = d.ActionId, Title = d.Title, Badge = d.Badge,
                    GainText = TweakLibrary.GainTextOf(d.ActionId)
                });
            var sb = new StringBuilder();
            sb.AppendLine("# R33333AD Optimizer — rapport");
            sb.AppendLine();
            sb.AppendLine($"- Version : {UpdateService.CurrentVersion}");
            sb.AppendLine($"- Date : {DateTime.Now:yyyy-MM-dd HH:mm}");
            sb.AppendLine($"- Admin : {(AdminHelper.IsAdmin() ? "oui" : "non")}");
            sb.AppendLine($"- Modifications journalisées : {TweakJournal.Count()}");
            sb.AppendLine($"- Benchmark : {BenchmarkService.BaselineSummary()}");
            sb.AppendLine();
            sb.AppendLine("## Historique benchmark (20 derniers)");
            sb.AppendLine();
            sb.AppendLine("| Date | Score | CPU ms | RAM ms | Disque ms |");
            sb.AppendLine("|---|---|---|---|---|");
            var hist = BenchmarkService.LoadHistory();
            if (hist.Count == 0) sb.AppendLine("| — | — | — | — | — |");
            foreach (var r in hist.TakeLast(20))
                sb.AppendLine($"| {r.Date[..16].Replace('T', ' ')} | {r.Score}/100 | {r.CpuMs:0} | {r.RamMs:0} | {r.DiskMs:0} |");
            sb.AppendLine();
            sb.AppendLine("## Gains mesurés (moyenne des 5 dernières mesures)");
            sb.AppendLine();
            sb.AppendLine("| Tweak | Avant | Après | Gain | Mesures |");
            sb.AppendLine("|---|---|---|---|---|");
            var gains = FpsGainService.LoadHistory();
            if (gains.Count == 0) sb.AppendLine("| _aucune mesure_ | — | — | — | — |");
            foreach (var (id, list) in gains.OrderByDescending(kv => kv.Value.Average(x => x.GainPct)))
            {
                string title = TweakLibrary.ById(id)?.Title ?? id;
                var last = list[^1];
                sb.AppendLine($"| {title} | {last.Before:0} FPS | {last.After:0} FPS | {FpsGainService.FormatMeasured(list.Average(x => x.GainPct))} | {list.Count} |");
            }
            sb.AppendLine();
            sb.AppendLine("## Tweaks (estimation indicative, non additive)");
            sb.AppendLine();
            sb.AppendLine("| Tweak | Type | Gain estimé | Appliqué |");
            sb.AppendLine("|---|---|---|---|");
            foreach (var t in tools)
                sb.AppendLine($"| {t.Title} | {t.Badge} | {t.GainText} | {(t.IsApplied ? "oui" : "non")} |");
            sb.AppendLine();
            sb.AppendLine("## Journal par action (qui a modifié quoi)");
            sb.AppendLine();
            sb.AppendLine("| Action | Modifs |");
            sb.AppendLine("|---|---|");
            var byAction = TweakJournal.Load()
                .GroupBy(e => string.IsNullOrEmpty(e.ActionId) ? "inconnu" : e.ActionId)
                .OrderByDescending(g => g.Count());
            if (!byAction.Any()) sb.AppendLine("| _journal vide_ | 0 |");
            foreach (var g in byAction)
                sb.AppendLine($"| {TweakLibrary.ById(g.Key)?.Title ?? g.Key} | {g.Count()} |");
            sb.AppendLine();
            sb.AppendLine("_Estimations indicatives : varient selon PC/jeu, non additives._");

            string dest = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"R33333AD-rapport-{DateTime.Now:yyyyMMdd-HHmm}.md");
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.WriteAllText(dest, sb.ToString());
            Logger.Info($"Rapport exporté: {dest}");
            return new TweakResult(true, dest);
        }
        catch (Exception ex)
        {
            Logger.Error("Rapport export", ex);
            return new TweakResult(false, "Export échoué : " + ex.Message);
        }
    }
}
