using Microsoft.Win32;

namespace FPSBooster.App.Services;

/// <summary>Écriture registre sécurisée avec log + backup de l'ancienne valeur.</summary>
public static class RegistryHelper
{
    public static bool Set(RegistryHive hive, string subKey, string valueName, object value, RegistryValueKind kind)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.CreateSubKey(subKey, writable: true);
            if (key == null) return false;

            object? old = null;
            RegistryValueKind oldKind = kind;
            bool existed = false;
            try
            {
                old = key.GetValue(valueName);
                if (old != null)
                {
                    existed = true;
                    try { oldKind = key.GetValueKind(valueName); }
                    catch (Exception ex) { Logger.Warn($"REG kind '{subKey}::{valueName}': {ex.Message}"); }
                }
            }
            catch (Exception ex) { Logger.Warn($"REG lecture '{subKey}::{valueName}': {ex.Message}"); }

            key.SetValue(valueName, value, kind);
            TweakJournal.AppendReg(hive, subKey, valueName, kind, existed, TweakJournal.Encode(old, oldKind));
            RegBackupService.AppendSet(hive, subKey, valueName, old, oldKind, existed);
            Logger.Info($"REG {hive}\\{subKey} :: {valueName} = {value} (avant: {old ?? "<absent>"})");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"REG FAIL {hive}\\{subKey} :: {valueName}", ex);
            return false;
        }
    }

    public static bool DeleteValue(RegistryHive hive, string subKey, string valueName)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(subKey, writable: true);
            if (key != null)
            {
                object? old = null;
                RegistryValueKind oldKind = RegistryValueKind.String;
                bool existed = false;
                try
                {
                    old = key.GetValue(valueName);
                    if (old != null)
                    {
                        existed = true;
                        try { oldKind = key.GetValueKind(valueName); }
                    catch (Exception ex) { Logger.Warn($"REG kind '{subKey}::{valueName}': {ex.Message}"); }
                    }
                }
                catch (Exception ex) { Logger.Warn($"REG DEL lecture '{subKey}::{valueName}': {ex.Message}"); }
                if (existed)
                {
                    TweakJournal.AppendReg(hive, subKey, valueName, oldKind, true, TweakJournal.Encode(old, oldKind));
                    RegBackupService.AppendSet(hive, subKey, valueName, old, oldKind, true);
                }
                key.DeleteValue(valueName, throwOnMissingValue: false);
            }
            Logger.Info($"REG DEL {hive}\\{subKey} :: {valueName}");
            return true;
        }
        catch (Exception ex)
        {
            Logger.Error($"REG DEL FAIL {hive}\\{subKey}", ex);
            return false;
        }
    }

    // Helpers courts
    public static bool SetHKCU(string subKey, string name, int value) =>
        Set(RegistryHive.CurrentUser, subKey, name, value, RegistryValueKind.DWord);

    public static bool SetHKCUString(string subKey, string name, string value) =>
        Set(RegistryHive.CurrentUser, subKey, name, value, RegistryValueKind.String);

    public static bool SetHKLM(string subKey, string name, int value) =>
        Set(RegistryHive.LocalMachine, subKey, name, value, RegistryValueKind.DWord);

    public static bool SetHKLMBinary(string subKey, string name, byte[] value) =>
        Set(RegistryHive.LocalMachine, subKey, name, value, RegistryValueKind.Binary);
}
