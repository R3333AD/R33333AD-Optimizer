using System.IO;
using System.Text;
using Microsoft.Win32;

namespace FPSBooster.App.Services;

/// <summary>Sauvegarde .reg exportable de chaque modif registre (restaurable à la main
/// via regedit si l'app crash). Un fichier par jour : regbackup-AAAAMMJJ.reg.</summary>
public static class RegBackupService
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", $"regbackup-{DateTime.Now:yyyyMMdd}.reg");

    private static readonly object Lock = new();
    private static string _initFile = "";

    public static string BackupPath => FilePath;

    /// <summary>Journalise l'ancienne valeur (ou sa suppression si elle n'existait pas).</summary>
    public static void AppendSet(RegistryHive hive, string subKey, string name, object? oldValue, RegistryValueKind oldKind, bool existed)
    {
        try
        {
            var sb = new StringBuilder();
            sb.AppendLine($"[{HiveName(hive)}\\{subKey}]");
            sb.AppendLine(existed ? FormatValue(name, oldValue, oldKind) : $"\"{Escape(name)}\"=-");
            AppendText(sb.ToString());
            Logger.Info($"REGBACKUP {hive}\\{subKey} :: {name}");
        }
        catch (Exception ex) { Logger.Warn($"RegBackup: {ex.Message}"); }
    }

    private static void AppendText(string text)
    {
        lock (Lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            if (_initFile != FilePath)
            {
                _initFile = FilePath;
                if (!File.Exists(FilePath) || new FileInfo(FilePath).Length == 0)
                    File.WriteAllText(FilePath, "Windows Registry Editor Version 5.00\r\n\r\n");
            }
            File.AppendAllText(FilePath, text + "\r\n");
        }
    }

    private static string HiveName(RegistryHive hive) => hive switch
    {
        RegistryHive.CurrentUser => "HKEY_CURRENT_USER",
        RegistryHive.LocalMachine => "HKEY_LOCAL_MACHINE",
        RegistryHive.ClassesRoot => "HKEY_CLASSES_ROOT",
        RegistryHive.Users => "HKEY_USERS",
        RegistryHive.CurrentConfig => "HKEY_CURRENT_CONFIG",
        _ => hive.ToString(),
    };

    private static string Escape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    private static string HexBytes(byte[] b) => string.Join(",", b.Select(x => x.ToString("x2")));

    private static string FormatValue(string name, object? value, RegistryValueKind kind)
    {
        string n = $"\"{Escape(name)}\"";
        try
        {
            switch (kind)
            {
                case RegistryValueKind.DWord:
                    return $"{n}=dword:{unchecked((uint)Convert.ToInt32(value)):x8}";
                case RegistryValueKind.QWord:
                    return $"{n}=hex(b):{HexBytes(BitConverter.GetBytes(Convert.ToInt64(value)))}";
                case RegistryValueKind.String:
                    return FormatRegString(n, Convert.ToString(value) ?? "");
                case RegistryValueKind.ExpandString:
                    return $"{n}=hex(2):{HexBytes(Encoding.Unicode.GetBytes(Convert.ToString(value) ?? ""))},00,00";
                case RegistryValueKind.Binary:
                    return value is byte[] bin
                        ? $"{n}=hex:{HexBytes(bin)}"
                        : $"{n}=hex(0):";
                case RegistryValueKind.MultiString:
                    if (value is string[] arr)
                    {
                        var bytes = arr.SelectMany(s => Encoding.Unicode.GetBytes(s + "\0")).ToArray();
                        return $"{n}=hex(7):{HexBytes(bytes)},00,00";
                    }
                    return $"{n}=hex(7):00,00";
                default:
                    return FormatRegString(n, value?.ToString() ?? "");
            }
        }
        catch (Exception ex)
        {
            Logger.Warn($"RegBackup format '{name}': {ex.Message}");
            return $"{n}=\"\"";
        }
    }

    private static string FormatRegString(string n, string s)
    {
        // Les chaînes avec caractères de contrôle ne passent pas en clair dans un .reg
        if (s.Any(c => c == '\r' || c == '\n' || c == '\0'))
            return $"{n}=hex(1):{HexBytes(Encoding.Unicode.GetBytes(s))},00,00";
        return $"{n}=\"{Escape(s)}\"";
    }
}
