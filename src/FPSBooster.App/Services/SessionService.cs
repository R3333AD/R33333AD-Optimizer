using System.Diagnostics;
using FPSBooster.App.Models;
using Microsoft.Win32;

namespace FPSBooster.App.Services;

/// <summary>Session gaming : coupe notifs + pause Windows Update + bloat. Stop = tout restaure.</summary>
public static class SessionService
{
    // Services arrêtés PAR la session (pour ne redémarrer que ceux-là au Stop)
    private static bool _stoppedWuAuServ;
    private static bool _stoppedUsoSvc;

    private static async Task<bool> IsServiceRunningAsync(string svc)
    {
        try
        {
            var (code, stdout, _) = await CmdHelper.RunAsync("sc.exe", $"query {svc}", 10_000);
            return code == 0 && stdout.Contains("RUNNING", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex)
        {
            Logger.Warn($"Session état service '{svc}': {ex.Message}");
            return false;
        }
    }

    public static async Task<TweakResult> StartAsync()
    {
        try
        {
            WindowsTweakService.GamingDoNotDisturb(true);
            // Pause Windows Update le temps de la session (seuls les services arrêtés par nous sont redémarrés au Stop)
            _stoppedWuAuServ = false;
            _stoppedUsoSvc = false;
            if (await IsServiceRunningAsync("wuauserv"))
            {
                var (code, _, _) = await CmdHelper.RunAsync("net.exe", "stop wuauserv");
                _stoppedWuAuServ = code == 0;
                if (!_stoppedWuAuServ) Logger.Warn("Session: arrêt wuauserv échoué");
            }
            if (await IsServiceRunningAsync("UsoSvc"))
            {
                var (code, _, _) = await CmdHelper.RunAsync("net.exe", "stop UsoSvc");
                _stoppedUsoSvc = code == 0;
                if (!_stoppedUsoSvc) Logger.Warn("Session: arrêt UsoSvc échoué");
            }
            int killed = ProcessService.KillBloat(includeBrowsers: false);
            CleanerService.TrimMemory();
            await NetworkService.FlushDnsAsync();
            return new(true, $"Session jeu ON : notifs OFF, WU en pause, {killed} process fermés");
        }
        catch (Exception ex)
        {
            Logger.Error("Session start", ex);
            return new(false, "Session échouée : " + ex.Message);
        }
    }

    public static async Task<TweakResult> StopAsync()
    {
        try
        {
            WindowsTweakService.GamingDoNotDisturb(false);
            if (_stoppedWuAuServ)
            {
                await CmdHelper.RunAsync("net.exe", "start wuauserv");
                _stoppedWuAuServ = false;
            }
            if (_stoppedUsoSvc)
            {
                await CmdHelper.RunAsync("net.exe", "start UsoSvc");
                _stoppedUsoSvc = false;
            }
            return new(true, "Session jeu OFF : notifs ON, WU repris");
        }
        catch (Exception ex)
        {
            Logger.Error("Session stop", ex);
            return new(false, "Stop échoué : " + ex.Message);
        }
    }

    /// <summary>Force le GPU haute perf pour chaque jeu détecté en cours (NVIDIA/AMD/Intel).</summary>
    public static TweakResult GpuPreferenceForRunningGames()
    {
        try
        {
            var games = ProcessService.DetectRunningGames();
            if (games.Length == 0)
                return new(false, "Aucun jeu détecté (lance-le d'abord)");
            int done = 0;
            foreach (var name in games)
            {
                try
                {
                    foreach (var p in Process.GetProcessesByName(name))
                    {
                        string pname;
                        try { pname = p.ProcessName; }
                        catch (Exception ex)
                        {
                            Logger.Warn($"GpuPreference lecture nom ('{name}'): {ex.Message}");
                            p.Dispose();
                            continue;
                        }
                        try
                        {
                            string? path = p.MainModule?.FileName;
                            if (string.IsNullOrEmpty(path))
                            {
                                Logger.Warn($"GpuPreference '{pname}': chemin inaccessible (anti-cheat ?)");
                                continue;
                            }
                            RegistryHelper.Set(RegistryHive.CurrentUser,
                                @"Software\Microsoft\DirectX\UserGpuPreferences",
                                path, "GpuPreference=2;", RegistryValueKind.String);
                            done++;
                            Logger.Info($"GPU haute perf: {path}");
                        }
                        catch (Exception ex) { Logger.Warn($"GpuPreference '{pname}': {ex.Message}"); }
                        finally { p.Dispose(); }
                    }
                }
                catch (Exception ex) { Logger.Warn($"GpuPreference jeu '{name}': {ex.Message}"); }
            }
            return done > 0
                ? new(true, $"GPU haute perf appliqué ({done} jeu(x) : {string.Join(", ", games)})")
                : new(false, "Chemins jeux inaccessibles (anti-cheat ?)");
        }
        catch (Exception ex)
        {
            Logger.Error("GpuPreference", ex);
            return new(false, "GPU pref échoué : " + ex.Message);
        }
    }
}
