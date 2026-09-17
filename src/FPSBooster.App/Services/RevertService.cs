using FPSBooster.App.Models;
using Microsoft.Win32;

namespace FPSBooster.App.Services;

/// <summary>Annule toutes les modifs journalisées (ordre inverse). Fichiers supprimés non récupérables.</summary>
public static class RevertService
{
    public static async Task<(int Restored, int Failed)> RevertAllAsync()
    {
        var entries = TweakJournal.Load();
        entries.Reverse();
        int ok = 0, fail = 0;
        foreach (var e in entries)
        {
            try
            {
                bool done = e.Area switch
                {
                    "reg" => RevertReg(e),
                    "service" => await RevertServiceModeAsync(e),
                    "power" => await RevertPowerAsync(e),
                    _ => false,
                };
                if (done) ok++; else fail++;
            }
            catch (Exception ex)
            {
                fail++;
                Logger.Error($"Revert {e.Area} {e.Hive}\\{e.Key}::{e.Name}", ex);
            }
        }
        TweakJournal.Archive();
        Logger.Info($"RevertAll: {ok} restaurés / {fail} échecs sur {entries.Count}");
        return (ok, fail);
    }

    /// <summary>Annule UNE seule entrée (revert sélectif) et la retire du journal.</summary>
    public static async Task<bool> RevertOneAsync(JournalEntry e)
    {
        try
        {
            bool done = e.Area switch
            {
                "reg" => RevertReg(e),
                "service" => await RevertServiceModeAsync(e),
                "power" => await RevertPowerAsync(e),
                _ => false,
            };
            if (done)
            {
                TweakJournal.Remove(e);
                Logger.Info($"RevertOne OK {e.Area} {e.Hive}\\{e.Key}::{e.Name}");
            }
            return done;
        }
        catch (Exception ex)
        {
            Logger.Error($"RevertOne {e.Area} {e.Hive}\\{e.Key}::{e.Name}", ex);
            return false;
        }
    }

    private static bool RevertReg(JournalEntry e)
    {
        if (!Enum.TryParse<RegistryHive>(e.Hive, out var hive)) return false;
        if (!Enum.TryParse<RegistryValueKind>(e.Kind, out var kind))
            kind = RegistryValueKind.String;
        // Essaie Registry64 en premier, puis Registry32 si nécessaire (sans dupliquer la logique)
        if (TryRevertRegView(hive, RegistryView.Registry64, e, kind, viewName: "64-bit", out Exception? first))
            return true;
        Logger.Warn($"RevertReg 64-bit échoue ({e.Key}::{e.Name}), tentative 32-bit: {first?.Message}");
        if (TryRevertRegView(hive, RegistryView.Registry32, e, kind, viewName: "32-bit", out Exception? second))
            return true;
        Logger.Error($"RevertReg {e.Key}::{e.Name}", second);
        return false;
    }

    private static bool TryRevertRegView(RegistryHive hive, RegistryView view, JournalEntry e, RegistryValueKind kind, string viewName, out Exception? error)
    {
        error = null;
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            if (e.Existed)
            {
                using var key = baseKey.CreateSubKey(e.Key, writable: true);
                if (key == null) return false;
                var value = TweakJournal.Decode(e.OldValue, kind);
                if (value == null) return false;
                key.SetValue(e.Name, value, kind);
                Logger.Info($"REVERT REG ({viewName}) {e.Hive}\\{e.Key} :: {e.Name} = {e.OldValue}");
            }
            else
            {
                using var key = baseKey.OpenSubKey(e.Key, writable: true);
                key?.DeleteValue(e.Name, throwOnMissingValue: false);
                Logger.Info($"REVERT REG DEL ({viewName}) {e.Hive}\\{e.Key} :: {e.Name} (recréée par nous -> supprimée)");
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex;
            return false;
        }
    }

    private static async Task<bool> RevertServiceModeAsync(JournalEntry e)
    {
        // e.Hive = nom du service, e.OldValue = ancien mode
        if (string.IsNullOrEmpty(e.OldValue)) return false;
        var (code, _, _) = await CmdHelper.RunAsync("sc.exe", $"config {e.Hive} start= {e.OldValue}");
        Logger.Info($"REVERT Service {e.Hive} start= {e.OldValue} exit={code}");
        return code == 0;
    }

    private static async Task<bool> RevertPowerAsync(JournalEntry e)
    {
        if (string.IsNullOrEmpty(e.OldValue)) return false;
        var (code, _, _) = await CmdHelper.PowerCfg($"-setactive {e.OldValue}");
        Logger.Info($"REVERT Power plan {e.OldValue} exit={code}");
        return code == 0;
    }
}
