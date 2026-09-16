using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace FPSBooster.App.Services;

public static partial class CleanerService
{
    [LibraryImport("psapi.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool EmptyWorkingSet(IntPtr hProcess);

    public static async Task<int> CleanTempAsync()
    {
        int deleted = 0;
        string[] dirs =
        {
            Path.GetTempPath(),
            @"C:\Windows\Temp",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Temp")
        };
        foreach (var dir in dirs.Distinct())
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.EnumerateFiles(dir))
                {
                    try { File.Delete(f); deleted++; }
                    catch { /* fichier verrouillé -> skip */ }
                }
                foreach (var d in Directory.EnumerateDirectories(dir))
                {
                    try { Directory.Delete(d, recursive: true); deleted++; }
                    catch (Exception ex) { Logger.Warn($"CleanTemp dossier '{d}': {ex.Message}"); }
                }
            }
            catch (Exception ex) { Logger.Error($"CleanTemp {dir}", ex); }
        }
        Logger.Info($"CleanTemp: {deleted} fichiers/dossiers supprimés");
        await Task.CompletedTask;
        return deleted;
    }

    /// <summary>Supprime les fichiers logs (Windows, AppData, crash dumps, nos logs). Retourne (fichiers, octets).</summary>
    public static async Task<(int Files, long Bytes)> CleanLogsAsync()
    {
        int files = 0;
        long bytes = 0;
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var targets = new (string Dir, string Pattern)[]
        {
            (Path.Combine(localAppData, "FPSBooster", "logs"), "*.log"), // nos logs
            (Path.GetTempPath(), "*.log"),
            (@"C:\Windows\Temp", "*.log"),
            (@"C:\Windows\Logs\CBS", "*.log"),  // logs servicing (parfois des Go)
            (@"C:\Windows\Logs\CBS", "*.cab"),  // archives de vieux logs CBS
            (Path.Combine(localAppData, "CrashDumps"), "*.dmp"),
            (localAppData, "*.log"),            // logs posés en racine AppData (niveau 1 only)
            (@"C:\Windows\SoftwareDistribution\DataStore\Logs", "*.log"),
        };
        foreach (var (dir, pattern) in targets)
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        bytes += new FileInfo(f).Length;
                        File.Delete(f);
                        files++;
                    }
                    catch { /* verrouillé -> skip */ }
                }
            }
            catch (Exception ex) { Logger.Error($"CleanLogs {dir}\\{pattern}", ex); }
        }
        Logger.Info($"CleanLogs: {files} fichiers, {FormatBytes(bytes)} libérés");
        await Task.CompletedTask;
        return (files, bytes);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024 * 1024):0.0} Go";
        if (bytes >= 1024 * 1024) return $"{bytes / (1024.0 * 1024):0.0} Mo";
        if (bytes >= 1024) return $"{bytes / 1024.0:0} Ko";
        return $"{bytes} o";
    }

    /// <summary>Vide les caches navigateurs (jamais historique/cookies/mdp). Fichiers verrouillés = sautés.</summary>
    public static async Task<(int Files, long Bytes)> CleanBrowsersAsync()
    {
        int files = 0, skipped = 0;
        long bytes = 0;
        string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);

        var cacheDirs = new List<string>();
        // Chrome + Edge (tous profils)
        foreach (var baseDir in new[] { Path.Combine(local, @"Google\Chrome\User Data"), Path.Combine(local, @"Microsoft\Edge\User Data") })
        {
            if (!Directory.Exists(baseDir)) continue;
            foreach (var profile in Directory.EnumerateDirectories(baseDir))
            {
                foreach (var sub in new[] { "Cache", "Code Cache", "GPUCache", "ShaderCache" })
                    cacheDirs.Add(Path.Combine(profile, sub));
                string sw = Path.Combine(profile, "Service Worker", "CacheStorage");
                cacheDirs.Add(sw);
            }
        }
        // Firefox (cache2 de chaque profil)
        string ffProfiles = Path.Combine(local, @"Mozilla\Firefox\Profiles");
        if (Directory.Exists(ffProfiles))
            foreach (var profile in Directory.EnumerateDirectories(ffProfiles))
                cacheDirs.Add(Path.Combine(profile, "cache2"));
        // Discord (+ Canary/PTB)
        foreach (var app in new[] { "discord", "discordcanary", "discordptb" })
        {
            string d = Path.Combine(roaming, app);
            if (!Directory.Exists(d)) continue;
            foreach (var sub in new[] { "Cache", "Code Cache", "GPUCache" })
                cacheDirs.Add(Path.Combine(d, sub));
        }

        foreach (var dir in cacheDirs.Distinct())
        {
            try
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        bytes += new FileInfo(f).Length;
                        File.Delete(f);
                        files++;
                    }
                    catch { skipped++; /* verrouillé (navigateur ouvert) -> skip */ }
                }
            }
            catch (Exception ex) { Logger.Error($"CleanBrowsers {dir}", ex); }
        }
        Logger.Info($"CleanBrowsers: {files} fichiers, {FormatBytes(bytes)}, {skipped} verrouillés (ferme les navigateurs pour 100%)");
        await Task.CompletedTask;
        return (files, bytes);
    }
    public static int TrimMemory()
    {
        int count = 0, failed = 0;
        foreach (var proc in Process.GetProcesses())
        {
            string pname;
            try { pname = proc.ProcessName; }
            catch (Exception ex)
            {
                failed++;
                Logger.Warn($"TrimMemory lecture nom process: {ex.Message}");
                proc.Dispose();
                continue;
            }
            try
            {
                if (EmptyWorkingSet(proc.Handle)) count++;
                else failed++;
            }
            catch (Exception ex)
            {
                failed++;
                Logger.Warn($"TrimMemory '{pname}' impossible: {ex.Message}");
            }
            finally { proc.Dispose(); }
        }
        // Cache DNS (fire-and-forget avec log en cas d'échec, sans bloquer l'appelant)
        _ = CmdHelper.Ipconfig("/flushdns").ContinueWith(t =>
        {
            if (t.IsFaulted) Logger.Warn($"TrimMemory flushdns: {t.Exception?.GetBaseException().Message}");
        }, TaskScheduler.Default);
        Logger.Info($"TrimMemory: {count} process trimmés, {failed} échecs");
        return count;
    }

    public static async Task ClearDeliveryOptimizationAsync()
    {
        // Vide le cache Windows Update / Delivery Optimization
        await CmdHelper.RunAsync("net.exe", "stop DoSvc");
        try
        {
            var doCache = @"C:\Windows\SoftwareDistribution\DeliveryOptimization";
            if (Directory.Exists(doCache))
                foreach (var f in Directory.EnumerateFiles(doCache, "*", SearchOption.AllDirectories))
                    try { File.Delete(f); }
                    catch (Exception ex) { Logger.Warn($"ClearDeliveryOptimization '{f}': {ex.Message}"); }
        }
        catch (Exception ex) { Logger.Error("ClearDeliveryOptimization", ex); }
        await CmdHelper.RunAsync("net.exe", "start DoSvc");
    }

    public static async Task<string> ClearRecycleBinAsync()
    {
        // Si déjà vide, Clear-RecycleBin sort en erreur ("introuvable") -> on vérifie d'abord
        var (c0, out0, _) = await CmdHelper.Powershell(
            "(New-Object -ComObject Shell.Application).NameSpace(10).Items().Count", 15_000);
        if (c0 == 0 && int.TryParse(out0.Trim(), out int n) && n == 0)
        {
            Logger.Info("ClearRecycleBin: déjà vide");
            return "Corbeille déjà vide";
        }
        var (code, _, _) = await CmdHelper.Powershell(
            "Clear-RecycleBin -Force -ErrorAction SilentlyContinue", 30_000);
        Logger.Info($"ClearRecycleBin exit={code}");
        return code == 0 ? "Corbeille vidée" : "Corbeille traitée";
    }

    /// <summary>Vide le cache FiveM (garde game-storage). Retourne le nb d'entrées supprimées.</summary>
    public static int CleanFiveMCache()
    {
        int cleaned = 0;
        string fivemApp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FiveM", "FiveM.app");
        string[] cacheDirs =
        {
            Path.Combine(fivemApp, "cache"),
            Path.Combine(fivemApp, "logs"),
            Path.Combine(fivemApp, "crashes")
        };
        foreach (var d in cacheDirs)
        {
            try
            {
                if (!Directory.Exists(d)) continue;
                foreach (var sub in Directory.EnumerateFileSystemEntries(d))
                {
                    try
                    {
                        if (sub.Contains("game-storage")) continue;
                        if (Directory.Exists(sub)) Directory.Delete(sub, true);
                        else File.Delete(sub);
                        cleaned++;
                    }
                    catch (Exception ex) { Logger.Warn($"CleanFiveMCache '{sub}': {ex.Message}"); }
                }
            }
            catch (Exception ex) { Logger.Error($"CleanFiveMCache {d}", ex); }
        }
        Logger.Info($"CleanFiveMCache: {cleaned} entrées");
        return cleaned;
    }
}
