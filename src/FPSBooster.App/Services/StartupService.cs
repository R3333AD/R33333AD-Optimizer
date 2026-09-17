using System.IO;
using Microsoft.Win32;

namespace FPSBooster.App.Services;

/// <summary>Nettoie les programmes au démarrage (clés Run). Backup des valeurs en log.</summary>
public static class StartupService
{
    // Noms de valeurs exacts (comparaison stricte : jamais de sous-chaîne pour éviter les faux positifs)
    private static readonly string[] BloatValues =
    {
        "OneDrive", "Teams", "Spotify", "Discord", "Skype",
        "Adobe Creative Cloud", "AdobeAAMUpdater", "CCXProcess",
        "Cortana", "XboxGameBar", "EpicGamesLauncher", "Steam"
    };

    private static readonly (RegistryHive Hive, RegistryView View, string Key)[] RunKeys =
    {
        (RegistryHive.CurrentUser, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.CurrentUser, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\Run"),
        (RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\Run"),
    };

    // Démarrage auto via tâche planifiée (pas de prompt UAC à l'ouverture, contrairement à la clé Run
    // combinée au manifeste requireAdministrator).
    private const string TaskName = "R33333ADOptimizer";

    public static string AppExePath =>
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "R33333ADOptimizer.exe");

    public static async Task<bool> IsAutoStartAsync()
    {
        try
        {
            // Silencieux : tâche absente = état normal, pas une erreur
            var (code, stdout, _) = await CmdHelper.RunAsync("schtasks.exe", $"/Query /TN \"{TaskName}\"", 10_000, silent: true);
            return code == 0 && stdout.Contains(TaskName, StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Logger.Warn($"AutoStart état: {ex.Message}");
            return false;
        }
    }

    public static async Task<bool> SetAutoStartAsync(bool on)
    {
        try
        {
            if (on)
            {
                var (code, _, err) = await CmdHelper.RunAsync("schtasks.exe",
                    $"/Create /TN \"{TaskName}\" /TR \"\\\"{AppExePath}\\\"\" /SC ONLOGON /RL HIGHEST /F", 20_000);
                Logger.Info($"AutoStart ON exit={code} {err.Trim()}");
                return code == 0;
            }
            var (del, _, _) = await CmdHelper.RunAsync("schtasks.exe", $"/Delete /TN \"{TaskName}\" /F", 10_000);
            Logger.Info($"AutoStart OFF exit={del}");
            return del == 0;
        }
        catch (Exception ex)
        {
            Logger.Error("AutoStart", ex);
            return false;
        }
    }

    public static int RemoveBloatStartup()
    {
        int removed = 0;
        foreach (var (hive, view, key) in RunKeys)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var run = baseKey.OpenSubKey(key, writable: true);
                if (run == null) continue;
                foreach (var name in run.GetValueNames())
                {
                    if (!BloatValues.Any(b => name.Equals(b, StringComparison.OrdinalIgnoreCase)))
                        continue;
                    try
                    {
                        var old = run.GetValue(name);
                        run.DeleteValue(name, throwOnMissingValue: false);
                        removed++;
                        Logger.Info($"STARTUP DEL {hive}\\{key} :: {name} = {old}");
                    }
                    catch (Exception ex) { Logger.Error($"STARTUP DEL {name}", ex); }
                }
            }
            catch (Exception ex) { Logger.Error($"STARTUP scan {hive}\\{key}", ex); }
        }
        Logger.Info($"Startup bloat supprimé: {removed}");
        return removed;
    }
}
