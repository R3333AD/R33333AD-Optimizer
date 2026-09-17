using System.IO;
using System.Text;
using System.Text.Json;
using FPSBooster.App.Models;
using Microsoft.Win32;

namespace FPSBooster.App.Services;

/// <summary>Journal JSONL des modifs réversibles + restauration.</summary>
public static class TweakJournal
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", "journal.jsonl");
    private static readonly object Lock = new();

    /// <summary>Action en cours d'exécution (positionné par ToolRunner) : chaque entrée
    /// journalisée sait par quel bouton elle a été créée. Suit le flux async.</summary>
    public static readonly AsyncLocal<string?> CurrentAction = new();

    public static void AppendReg(RegistryHive hive, string subKey, string name, RegistryValueKind kind, bool existed, string? oldValue)
    {
        Append(new JournalEntry(DateTime.Now.ToString("o"), "reg", hive.ToString(), subKey, name, kind.ToString(), existed, oldValue, CurrentAction.Value));
    }

    public static void AppendService(string service, string oldMode)
    {
        Append(new JournalEntry(DateTime.Now.ToString("o"), "service", service, "", "", "", true, oldMode, CurrentAction.Value));
    }

    public static void AppendPowerPlan(string oldGuid)
    {
        Append(new JournalEntry(DateTime.Now.ToString("o"), "power", oldGuid, "", "", "", true, oldGuid, CurrentAction.Value));
    }

    private static void Append(JournalEntry e)
    {
        try
        {
            lock (Lock)
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                File.AppendAllText(Path, JsonSerializer.Serialize(e) + "\n");
            }
        }
        catch (Exception ex) { Logger.Error("Journal append", ex); }
    }

    public static List<JournalEntry> Load()
    {
        var list = new List<JournalEntry>();
        try
        {
            lock (Lock)
            {
                if (!File.Exists(Path)) return list;
                foreach (var line in File.ReadAllLines(Path))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try { var e = JsonSerializer.Deserialize<JournalEntry>(line); if (e != null) list.Add(e); }
                    catch (Exception ex) { Logger.Warn($"Journal ligne illisible ignorée: {ex.Message}"); }
                }
            }
        }
        catch (Exception ex) { Logger.Error("Journal load", ex); }
        return list;
    }

    public static int Count() => Load().Count;

    public static void Archive()
    {
        try
        {
            lock (Lock)
            {
                if (!File.Exists(Path)) return;
                string bak = Path + $".{DateTime.Now:yyyyMMdd-HHmmss}.reverted";
                File.Move(Path, bak);
                Logger.Info($"Journal archivé: {bak}");
            }
        }
        catch (Exception ex) { Logger.Error("Journal archive", ex); }
    }

    /// <summary>Retire la PREMIÈRE entrée égale (revert sélectif). Retourne true si trouvée.</summary>
    public static bool Remove(JournalEntry target)
    {
        try
        {
            lock (Lock)
            {
                if (!File.Exists(Path)) return false;
                var lines = File.ReadAllLines(Path).ToList();
                for (int i = lines.Count - 1; i >= 0; i--)
                {
                    if (string.IsNullOrWhiteSpace(lines[i])) continue;
                    try
                    {
                        var e = JsonSerializer.Deserialize<JournalEntry>(lines[i]);
                        if (e == null || !e.Equals(target)) continue;
                        lines.RemoveAt(i);
                        File.WriteAllLines(Path, lines);
                        Logger.Info($"Journal retiré: {target.Area} {target.Hive}\\{target.Key}::{target.Name}");
                        return true;
                    }
                    catch (Exception ex) { Logger.Warn($"Journal remove ligne: {ex.Message}"); }
                }
                return false;
            }
        }
        catch (Exception ex)
        {
            Logger.Error("Journal remove", ex);
            return false;
        }
    }

    // ---------- encodage anciennes valeurs ----------

    public static string? Encode(object? value, RegistryValueKind kind)
    {
        try
        {
            if (value == null) return null;
            return kind switch
            {
                RegistryValueKind.Binary => Convert.ToBase64String((byte[])value),
                RegistryValueKind.MultiString => Convert.ToBase64String(Encoding.Unicode.GetBytes(string.Join("\0", (string[])value))),
                _ => value.ToString(),
            };
        }
        catch { return null; }
    }

    public static object? Decode(string? encoded, RegistryValueKind kind)
    {
        if (encoded == null) return null;
        return kind switch
        {
            RegistryValueKind.DWord => int.Parse(encoded),
            RegistryValueKind.QWord => long.Parse(encoded),
            RegistryValueKind.Binary => Convert.FromBase64String(encoded),
            RegistryValueKind.MultiString => Encoding.Unicode.GetString(Convert.FromBase64String(encoded)).Split('\0'),
            _ => encoded,
        };
    }
}
