using System.IO;
using Microsoft.Win32;

namespace FPSBooster.App.Services;

/// <summary>Vérifie les prérequis au démarrage (admin, registre, outils système, PresentMon, URL màj).</summary>
public static class PrereqService
{
    public record Prereq(string Name, bool Ok, string Detail);

    public static async Task<List<Prereq>> CheckAsync()
    {
        var list = new List<Prereq>();
        try
        {
            bool admin = AdminHelper.IsAdmin();
            list.Add(new Prereq("Admin", admin, admin ? "droits administrateur OK" : "relance en admin (clic droit)"));
            bool reg = await CanReadHklmAsync();
            list.Add(new Prereq("Registre HKLM", reg, reg ? "lecture HKLM OK" : "lecture HKLM refusée"));
            bool power = ExistsOnPath("powercfg.exe");
            list.Add(new Prereq("powercfg", power, power ? "présent" : "introuvable dans System32"));
            bool presentMon = File.Exists(FpsMeasureService.ToolPath);
            list.Add(new Prereq("PresentMon", presentMon, presentMon ? "présent" : $"absent ({FpsMeasureService.ToolDir})"));
            string url = SettingsService.Get("update_url", "").Trim();
            list.Add(new Prereq("URL màj", !string.IsNullOrEmpty(url),
                string.IsNullOrEmpty(url) ? "non configurée (settings → update_url)" : "configurée"));
            Logger.Info("Prérequis: " + string.Join(" | ", list.Select(p => $"{p.Name}={(p.Ok ? "OK" : "KO")}")));
        }
        catch (Exception ex) { Logger.Error("Prérequis", ex); }
        return list;
    }

    private static Task<bool> CanReadHklmAsync() => Task.Run(() =>
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
            using var s = baseKey.OpenSubKey("SOFTWARE");
            return s != null;
        }
        catch (Exception ex)
        {
            Logger.Warn($"Prérequis registre: {ex.Message}");
            return false;
        }
    });

    private static bool ExistsOnPath(string exe)
    {
        try
        {
            return File.Exists(Path.Combine(Environment.SystemDirectory, exe));
        }
        catch (Exception ex)
        {
            Logger.Warn($"Prérequis '{exe}': {ex.Message}");
            return false;
        }
    }
}
