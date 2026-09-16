using System.IO;
using FPSBooster.App.Models;

namespace FPSBooster.App.Services;

public static class BoostService
{
    public static async Task<TweakResult> Boost1Async()
    {
        try
        {
            int killed = ProcessService.KillBloat(includeBrowsers: false);
            int cleaned = await CleanerService.CleanTempAsync();
            CleanerService.TrimMemory();
            await PowerService.EnsureUltimatePlanAsync();
            RegistryHelper.SetHKCU(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 0);
            await NetworkService.FlushDnsAsync();
            return new(true, $"Boost 1 OK : {killed} process fermés, {cleaned} temp supprimés, Ultimate Perf ON");
        }
        catch (Exception ex)
        {
            Logger.Error("Boost1", ex);
            return new(false, "Boost 1 échoué : " + ex.Message);
        }
    }

    public static async Task<TweakResult> Boost3Async()
    {
        try
        {
            var r1 = await Boost1Async();
            if (!r1.Success) return r1;
            // Plus agressif : navigateurs inclus + SysMain/Search en manuel + effets visuels
            int killed2 = ProcessService.KillBloat(includeBrowsers: true);
            if (!WindowsTweakService.VisualEffectsBestPerformance()) return new(false, "Impossible de désactiver les effets visuels");
            if (!WindowsTweakService.DisableGameDVR()) return new(false, "Impossible de désactiver le Game DVR");
            if (!WindowsTweakService.SystemResponsivenessGpu()) return new(false, "Impossible d'optimiser la réactivité système");
            NetworkService.DisableNagle();
            await NetworkService.OptimizeTcpAsync();
            var (c1, _, _) = await CmdHelper.RunAsync("sc.exe", "config SysMain start= demand");
            if (c1 != 0) return new(false, "Impossible de configurer SysMain");
            var (c2, _, _) = await CmdHelper.RunAsync("sc.exe", "config DiagTrack start= disabled");
            if (c2 != 0) return new(false, "Impossible de configurer DiagTrack");
            return new(true, $"Boost 3 OK : agressif ({killed2} bloat supp.) + réseau + CPU débridé");
        }
        catch (Exception ex)
        {
            Logger.Error("Boost3", ex);
            return new(false, "Boost 3 échoué : " + ex.Message);
        }
    }

    public static async Task<TweakResult> BoostFiveMAsync()
    {
        try
        {
            // 1. Nettoie le cache FiveM (gros gain de stutter)
            int fxCleaned = CleanerService.CleanFiveMCache();
            // 2. Priorité FiveM/GTA si lancés
            int prio = ProcessService.SetGamePriority(new[] { "FiveM", "FiveM_GTAProcess", "GTA5" });
            // 3. Base FPS + réseau
            await Boost1Async();
            WindowsTweakService.DisableFSO();
            WindowsTweakService.OptimizeGraphicsCard();
            await NetworkService.FlushDnsAsync();
            return new(true, $"FiveM OK : cache vidé ({fxCleaned} entrées), priorité jeu x{prio}");
        }
        catch (Exception ex)
        {
            Logger.Error("BoostFiveM", ex);
            return new(false, "FiveM échoué : " + ex.Message);
        }
    }
}
