using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace FPSBooster.App.Services;

public record InstalledApp(
    string DisplayName,
    string? Version,
    string? Publisher,
    string? UninstallString,
    string? InstallLocation,
    string RegistryKey,
    RegistryHive Hive,
    RegistryView View,
    string? DisplaySize,
    long SizeKb); // taille brute en Ko (-1 = inconnue, pour le tri)

/// <summary>Désinstalleur style Revo : désinstall + scan/suppression des restes (zéro trace).</summary>
public static partial class UninstallerService
{
    private const string UninstallPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall";

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "windows", "microsoft", "corporation", "incorporated", "inc", "llc", "gmbh", "ltd", "limited",
        "software", "apps", "app", "program", "programs", "studios", "studio", "interactive",
        "entertainment", "labs", "technologies", "technology", "tech", "digital", "official",
        "team", "tools", "utility", "utilities", "suite", "package", "setup", "installer",
        "version", "free", "pro", "plus", "ultra", "client", "online", "game", "games", "gaming",
        "the", "for", "and", "with", "bit"
    };

    private static readonly HashSet<string> ProtectedProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "system", "registry", "smss", "csrss", "wininit", "services", "lsass", "winlogon",
        "explorer", "dwm", "sihost", "taskhostw", "svchost", "conhost", "fontdrvhost"
    };

    // ---------------- LISTE ----------------

    public static List<InstalledApp> GetInstalledApps()
    {
        var list = new List<InstalledApp>();
        var combos = new[] { (RegistryHive.LocalMachine, RegistryView.Registry64),
                             (RegistryHive.LocalMachine, RegistryView.Registry32),
                             (RegistryHive.CurrentUser, RegistryView.Registry64),
                             (RegistryHive.CurrentUser, RegistryView.Registry32) };
        foreach (var (hive, view) in combos)
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var uninst = baseKey.OpenSubKey(UninstallPath);
                if (uninst == null) continue;
                foreach (var sub in uninst.GetSubKeyNames())
                {
                    try
                    {
                        using var k = uninst.OpenSubKey(sub);
                        if (k == null) continue;
                        string? name = k.GetValue("DisplayName") as string;
                        if (string.IsNullOrWhiteSpace(name)) continue;
                        if (k.GetValue("SystemComponent") is int sc && sc == 1) continue;
                        if (k.GetValue("ParentKeyName") != null) continue; // patch / mise à jour
                        string? release = k.GetValue("ReleaseType") as string;
                        if (release is "Security Update" or "Update" or "Hotfix") continue;
                        if (IsUpdateEntry(name)) continue;

                        // Déduplique (même nom en 32/64 bits -> garde la 1re)
                        if (list.Any(a => a.DisplayName.Equals(name, StringComparison.OrdinalIgnoreCase))) continue;

                        int? sizeKb = k.GetValue("EstimatedSize") as int?;
                        list.Add(new InstalledApp(
                            name.Trim(),
                            k.GetValue("DisplayVersion") as string,
                            k.GetValue("Publisher") as string,
                            k.GetValue("UninstallString") as string,
                            k.GetValue("InstallLocation") as string,
                            sub, hive, view, FormatSize(sizeKb), sizeKb ?? -1));
                    }
                    catch (Exception ex) { Logger.Warn($"GetInstalledApps sous-clé ignorée '{sub}': {ex.Message}"); }
                }
            }
            catch (Exception ex) { Logger.Error($"GetInstalledApps {hive}/{view}", ex); }
        }
        Logger.Info($"GetInstalledApps: {list.Count} applications");
        return list.OrderBy(a => a.DisplayName).ToList();
    }

    private static bool IsUpdateEntry(string name) =>
        name.Contains("(KB", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Update for ", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Mise à jour ", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("Hotfix ", StringComparison.OrdinalIgnoreCase);

    private static string FormatSize(int? kb)
    {
        if (kb is null or <= 0) return "—";
        double mb = kb.Value / 1024.0;
        return mb >= 1024 ? $"{mb / 1024:0.0} Go" : $"{mb:0} Mo";
    }

    // ---------------- DÉSINSTALL ----------------

    public static async Task<(bool Success, string Message)> UninstallAsync(InstalledApp app)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(app.UninstallString))
                return (false, "Pas de commande de désinstallation (Composant système ?)");

            KillAppProcesses(app);
            var (exe, args, quiet) = BuildQuietCommand(app.UninstallString);
            Logger.Info($"UNINSTALL {app.DisplayName} -> {exe} {args} (silencieux={quiet})");

            if (string.IsNullOrWhiteSpace(exe))
                return (false, "Commande de désinstallation vide ou illisible");
            // Si c'est un chemin (contient \ ou / ou est enraciné) mais que le fichier n'existe pas,
            // on échoue avec un message clair au lieu de lancer "C:\Program".
            bool looksLikePath = exe.Contains('\\') || exe.Contains('/') || Path.IsPathRooted(exe);
            if (looksLikePath && !exe.Contains("msiexec", StringComparison.OrdinalIgnoreCase) && !File.Exists(exe))
            {
                Logger.Warn($"UNINSTALL exe introuvable: '{exe}' (source: '{app.UninstallString}')");
                return (false, $"Exécutable introuvable : {exe}");
            }

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = quiet
            };
            using var p = Process.Start(psi);
            if (p == null) return (false, "Impossible de lancer le désinstalleur");
            await p.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(10));

            Logger.Info($"UNINSTALL {app.DisplayName} exit={p.ExitCode}");
            await Task.Delay(2000); // laisse les enfants se terminer
            return p.ExitCode is 0 or 3010
                ? (true, $"Désinstallé{(p.ExitCode == 3010 ? " (redémarrage requis)" : "")} — scan des restes...")
                : (true, $"Désinstalleur terminé (code {p.ExitCode}) — scan des restes...");
        }
        catch (Exception ex)
        {
            Logger.Error($"Uninstall {app.DisplayName}", ex);
            return (false, "Échec : " + ex.Message);
        }
    }

    private static (string Exe, string Args, bool Quiet) BuildQuietCommand(string uninstallString)
    {
        if (string.IsNullOrWhiteSpace(uninstallString))
            return ("", "", false);
        var (exe, args) = SplitCommand(uninstallString.Trim());
        if (string.IsNullOrWhiteSpace(exe))
            return ("", args ?? "", false);
        // MSI -> silencieux natif
        if (exe.Contains("msiexec", StringComparison.OrdinalIgnoreCase))
        {
            var m = GuidRegex().Match(uninstallString);
            string guid = m.Success ? m.Value : args;
            return ("msiexec.exe", $"/x {guid} /qn /norestart", true);
        }
        // Inno Setup (unins000.exe) -> silencieux natif (sans jamais crasher sur un nom bizarre)
        string fileName;
        try { fileName = Path.GetFileName(exe); }
        catch (Exception ex)
        {
            Logger.Warn($"BuildQuietCommand GetFileName échoue pour '{exe}': {ex.Message}");
            return (exe, args, false);
        }
        if (fileName.StartsWith("unins", StringComparison.OrdinalIgnoreCase))
        {
            if (!args.Contains("SILENT", StringComparison.OrdinalIgnoreCase))
                args += " /VERYSILENT /SUPPRESSMSGBOXES /NORESTART";
            return (exe, args, true);
        }
        // Sinon : interactif (l'utilisateur clique dans l'assistant), puis on chasse les restes
        return (exe, args, false);
    }

    private static (string Exe, string Args) SplitCommand(string cmd)
    {
        if (string.IsNullOrWhiteSpace(cmd))
            return ("", "");
        // Expand %ProgramFiles%, %SystemRoot%, etc. avant de découper
        string expanded;
        try { expanded = Environment.ExpandEnvironmentVariables(cmd.Trim()); }
        catch (Exception ex)
        {
            Logger.Warn($"SplitCommand ExpandEnvironmentVariables échoue: {ex.Message}");
            expanded = cmd.Trim();
        }
        // Robuste aux chemins non quotés avec espaces : teste les préfixes les plus longs d'abord
        var tokens = Tokenize(expanded);
        for (int i = tokens.Count; i >= 1; i--)
        {
            string candidate = string.Join(" ", tokens.Take(i)).Trim('"', ' ');
            if (string.IsNullOrEmpty(candidate)) continue;
            try
            {
                if (File.Exists(candidate))
                    return (candidate, string.Join(" ", tokens.Skip(i)).Trim());
                // Candidat sans extension mais fichier .exe existant (ex: "setup" -> "setup.exe")
                if (!Path.HasExtension(candidate) && File.Exists(candidate + ".exe"))
                    return (candidate + ".exe", string.Join(" ", tokens.Skip(i)).Trim());
            }
            catch (Exception ex) { Logger.Warn($"SplitCommand File.Exists('{candidate}') échoue: {ex.Message}"); }
        }
        // Repli 1 : couper juste après le premier ".exe" trouvé (évite "C:\Program")
        try
        {
            var m = ExeSplitRegex().Match(expanded);
            if (m.Success)
            {
                string exe = m.Groups[1].Value.Trim().Trim('"', ' ');
                string args = m.Groups[2].Value.Trim();
                if (!string.IsNullOrWhiteSpace(exe))
                {
                    Logger.Warn($"SplitCommand repli .exe: '{cmd}' -> exe='{exe}'");
                    return (exe, args);
                }
            }
        }
        catch (Exception ex) { Logger.Warn($"SplitCommand regex .exe échoue: {ex.Message}"); }
        // Repli 2 : premier token qui ressemble à un exécutable (.exe/.msi/.bat/.cmd)
        for (int i = 1; i <= tokens.Count; i++)
        {
            string joined = string.Join(" ", tokens.Take(i)).Trim('"', ' ');
            string lower = joined.ToLowerInvariant();
            if (lower.EndsWith(".exe") || lower.EndsWith(".msi") || lower.EndsWith(".bat") || lower.EndsWith(".cmd"))
            {
                Logger.Warn($"SplitCommand repli extension: '{cmd}' -> exe='{joined}'");
                return (joined, string.Join(" ", tokens.Skip(i)).Trim());
            }
        }
        if (expanded.StartsWith("\""))
        {
            int end = expanded.IndexOf('"', 1);
            if (end > 0) return (expanded[1..end], expanded[(end + 1)..].Trim());
        }
        // Dernier repli : ne JAMAIS couper au premier espace d'un chemin.
        // On renvoie la commande entière pour un message d'erreur clair côté UninstallAsync.
        Logger.Warn($"SplitCommand impossible à découper proprement, commande entière conservée: '{cmd}'");
        return (expanded, "");
    }

    private static List<string> Tokenize(string cmd)
    {
        var tokens = new List<string>();
        var cur = new System.Text.StringBuilder();
        bool inQuotes = false;
        foreach (char c in cmd)
        {
            if (c == '"') { inQuotes = !inQuotes; cur.Append(c); }
            else if (c == ' ' && !inQuotes)
            {
                if (cur.Length > 0) { tokens.Add(cur.ToString()); cur.Clear(); }
            }
            else cur.Append(c);
        }
        if (cur.Length > 0) tokens.Add(cur.ToString());
        return tokens;
    }

    private static void KillAppProcesses(InstalledApp app)
    {
        foreach (var token in AppTokens(app).Where(t => t.Length >= 4))
        {
            try
            {
                foreach (var p in Process.GetProcesses())
                {
                    string pname = "?";
                    try { pname = p.ProcessName; } catch (Exception ex) { Logger.Warn($"Lecture nom process impossible: {ex.Message}"); }
                    try
                    {
                        if (ProtectedProcesses.Contains(pname)) continue;
                        if (pname.Contains(token, StringComparison.OrdinalIgnoreCase))
                        {
                            p.Kill();
                            Logger.Info($"Kill avant désinstall: {pname}");
                        }
                    }
                    catch (Exception ex) { Logger.Warn($"Kill process '{pname}' impossible: {ex.Message}"); }
                    finally { p.Dispose(); }
                }
            }
            catch (Exception ex) { Logger.Warn($"KillAppProcesses token '{token}': {ex.Message}"); }
        }
    }

    // ---------------- RESTES (ZÉRO TRACE) ----------------

    public record Leftover(string Kind, string Display, RegistryHive Hive, RegistryView View, string KeyPath, string? ValueName);

    public static List<Leftover> ScanLeftovers(InstalledApp app)
    {
        var found = new List<Leftover>();
        var tokens = AppTokens(app).ToList();
        if (tokens.Count == 0) return found;
        string longest = tokens.OrderByDescending(t => t.Length).First();

        // 1. Dossiers programmes
        string[] folderRoots =
        {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        };
        foreach (var root in folderRoots.Distinct())
            ScanFolders(root, tokens, longest, found, depth: 2);

        // 2. Menu Démarrer (.lnk)
        string[] startRoots =
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs"),
        };
        foreach (var root in startRoots)
            ScanShortcuts(root, tokens, longest, found);

        // 3. Registre Software (clés éditeur/app)
        ScanRegKeys(RegistryHive.CurrentUser, RegistryView.Registry64, @"Software", tokens, longest, found);
        ScanRegKeys(RegistryHive.LocalMachine, RegistryView.Registry64, @"Software", tokens, longest, found);
        ScanRegKeys(RegistryHive.LocalMachine, RegistryView.Registry32, @"Software", tokens, longest, found);
        // App Paths
        ScanRegKeys(RegistryHive.LocalMachine, RegistryView.Registry64, @"Software\Microsoft\Windows\CurrentVersion\App Paths", tokens, longest, found);
        ScanRegKeys(RegistryHive.LocalMachine, RegistryView.Registry32, @"Software\Microsoft\Windows\CurrentVersion\App Paths", tokens, longest, found);

        // 4. Clés Run (démarrage)
        ScanRunValues(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Run", longest, found);
        ScanRunValues(RegistryHive.LocalMachine, @"Software\Microsoft\Windows\CurrentVersion\Run", longest, found);

        // 5. La clé Uninstall elle-même si encore là
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(app.Hive, app.View);
            if (baseKey.OpenSubKey($"{UninstallPath}\\{app.RegistryKey}") != null)
                found.Add(new Leftover("Registre", $"{app.Hive}\\...\\Uninstall\\{app.RegistryKey}", app.Hive, app.View, $"{UninstallPath}\\{app.RegistryKey}", null));
        }
        catch (Exception ex) { Logger.Warn($"ScanLeftovers clé Uninstall '{app.DisplayName}': {ex.Message}"); }

        Logger.Info($"ScanLeftovers {app.DisplayName}: {found.Count} restes (sans plafond)");
        return found;
    }

    public static (int Deleted, int Failed) DeleteLeftovers(IEnumerable<Leftover> items)
    {
        int ok = 0, fail = 0;
        foreach (var item in items)
        {
            try
            {
                if (item.Kind is "Dossier" or "Raccourci")
                {
                    if (Directory.Exists(item.KeyPath)) Directory.Delete(item.KeyPath, recursive: true);
                    else if (File.Exists(item.KeyPath)) File.Delete(item.KeyPath);
                    else continue;
                }
                else
                {
                    using var baseKey = RegistryKey.OpenBaseKey(item.Hive, item.View);
                    if (item.ValueName != null)
                    {
                        using var k = baseKey.OpenSubKey(item.KeyPath, writable: true);
                        if (k == null) continue;
                        k.DeleteValue(item.ValueName, throwOnMissingValue: false);
                    }
                    else
                    {
                        baseKey.DeleteSubKeyTree(item.KeyPath, throwOnMissingSubKey: false);
                    }
                }
                ok++;
                Logger.Info($"RESTE SUPPRIMÉ [{item.Kind}] {item.Display}");
            }
            catch (Exception ex)
            {
                fail++;
                Logger.Error($"RESTE FAIL [{item.Kind}] {item.Display}", ex);
            }
        }
        return (ok, fail);
    }

    // ---------- helpers ----------

    private static List<string> AppTokens(InstalledApp app)
    {
        var words = new List<string>();
        words.AddRange(SplitWords(app.DisplayName));
        if (!string.IsNullOrWhiteSpace(app.Publisher)) words.AddRange(SplitWords(app.Publisher));
        return words.Where(w => w.Length >= 3 && !Stopwords.Contains(w) && !w.All(char.IsDigit))
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<string> SplitWords(string s) =>
        WordRegex().Split(s.ToLowerInvariant()).Where(w => w.Length >= 3);

    private static bool MatchesDir(string dirName, List<string> tokens, string longest)
    {
        string n = dirName.ToLowerInvariant();
        if (longest.Length >= 4 && n.Contains(longest)) return true;
        return tokens.Count(t => t.Length >= 4 && n.Contains(t)) >= 2;
    }

    private static void ScanFolders(string root, List<string> tokens, string longest, List<Leftover> found, int depth)
    {
        if (depth < 0 || string.IsNullOrEmpty(root) || !Directory.Exists(root)) return;
        string[] dirs;
        try { dirs = Directory.GetDirectories(root); }
        catch { return; }
        foreach (var d in dirs)
        {
            string name = Path.GetFileName(d);
            if (MatchesDir(name, tokens, longest))
                found.Add(new Leftover("Dossier", d, RegistryHive.CurrentUser, RegistryView.Default, d, null));
            else
                ScanFolders(d, tokens, longest, found, depth - 1);
        }
    }

    private static void ScanShortcuts(string root, List<string> tokens, string longest, List<Leftover> found)
    {
        if (!Directory.Exists(root)) return;
        try
        {
            foreach (var f in Directory.EnumerateFiles(root, "*.lnk", SearchOption.AllDirectories))
            {
                string name = Path.GetFileNameWithoutExtension(f);
                if (MatchesDir(name, tokens, longest))
                    found.Add(new Leftover("Raccourci", f, RegistryHive.CurrentUser, RegistryView.Default, f, null));
            }
        }
        catch (Exception ex) { Logger.Warn($"ScanShortcuts '{root}': {ex.Message}"); }
    }

    private static void ScanRegKeys(RegistryHive hive, RegistryView view, string parent, List<string> tokens, string longest, List<Leftover> found)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, view);
            using var p = baseKey.OpenSubKey(parent);
            if (p == null) return;
            foreach (var sub in p.GetSubKeyNames())
            {
                // Ne jamais toucher aux ruches Microsoft/Windows système
                if (sub.StartsWith("Microsoft", StringComparison.OrdinalIgnoreCase) ||
                    sub.StartsWith("Windows", StringComparison.OrdinalIgnoreCase) ||
                    sub.StartsWith("Classes", StringComparison.OrdinalIgnoreCase))
                    continue;
                if (MatchesDir(sub, tokens, longest))
                    found.Add(new Leftover("Registre", $"{hive}\\{parent}\\{sub}", hive, view, $"{parent}\\{sub}", null));
            }
        }
        catch (Exception ex) { Logger.Warn($"ScanRegKeys {hive}\\{parent}: {ex.Message}"); }
    }

    private static void ScanRunValues(RegistryHive hive, string key, string longest, List<Leftover> found)
    {
        if (longest.Length < 4) return;
        foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
        {
            try
            {
                using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                using var k = baseKey.OpenSubKey(key);
                if (k == null) continue;
                foreach (var v in k.GetValueNames())
                {
                    string? data = k.GetValue(v) as string;
                    if (v.Contains(longest, StringComparison.OrdinalIgnoreCase) ||
                        (data != null && data.Contains(longest, StringComparison.OrdinalIgnoreCase)))
                        found.Add(new Leftover("Démarrage", $"{hive}\\...\\Run :: {v}", hive, view, key, v));
                }
            }
            catch (Exception ex) { Logger.Warn($"ScanRunValues {hive}\\{key} [{view}]: {ex.Message}"); }
        }
    }

    [GeneratedRegex(@"\{[0-9A-Fa-f\-]{36}\}")]
    private static partial Regex GuidRegex();

    [GeneratedRegex(@"[^a-z0-9]+")]
    private static partial Regex WordRegex();

    [GeneratedRegex(@"^(.*?\.exe)""?\s*(.*)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ExeSplitRegex();

    /// <summary>Supprime complètement FPSBooster (logs, settings, registre, fichiers).
    /// À appeler juste avant de quitter l'app : les logs suivants recréeraient le dossier.</summary>
    public static (int FilesDeleted, int RegKeysDeleted) SelfUninstall()
    {
        int filesDeleted = 0, regDeleted = 0;
        try
        {
            // Supprimer le dossier de l'app (logs, settings, tools, cache)
            string appDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FPSBooster");
            if (Directory.Exists(appDir))
            {
                try { filesDeleted = Directory.EnumerateFiles(appDir, "*", SearchOption.AllDirectories).Count(); }
                catch (Exception ex) { Logger.Warn($"SelfUninstall comptage fichiers: {ex.Message}"); }
                Directory.Delete(appDir, recursive: true);
                Logger.Info("SelfUninstall: dossier FPSBooster supprimé (quittez l'app, sinon les logs le recréent)");
            }
        }
        catch (Exception ex) { Logger.Warn($"SelfUninstall dossier: {ex.Message}"); }
        try
        {
            // Supprimer les clés de registre de l'app
            foreach (var (hive, view) in new[] { (RegistryHive.CurrentUser, RegistryView.Registry64), (RegistryHive.LocalMachine, RegistryView.Registry64) })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    foreach (var sub in new[] { @"Software\FPSBooster", @"Software\Microsoft\Windows\CurrentVersion\Uninstall\R33333ADOptimizer", @"Software\Microsoft\Windows\CurrentVersion\Uninstall\{A3R3333A-D0PTI-MIZER-1100}_is1" })
                    {
                        try
                        {
                            using (var k = baseKey.OpenSubKey(sub)) { if (k == null) continue; }
                            baseKey.DeleteSubKeyTree(sub, throwOnMissingSubKey: false);
                            regDeleted++;
                        }
                        catch (Exception ex) { Logger.Warn($"SelfUninstall registre {hive}\\{sub}: {ex.Message}"); }
                    }
                }
                catch (Exception ex) { Logger.Warn($"SelfUninstall ruche {hive}/{view}: {ex.Message}"); }
            }
        }
        catch (Exception ex) { Logger.Warn($"SelfUninstall registre: {ex.Message}"); }
        Logger.Info($"SelfUninstall: {filesDeleted} fichiers, {regDeleted} clés registre supprimées");
        return (filesDeleted, regDeleted);
    }
}
