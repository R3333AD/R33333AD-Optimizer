using Microsoft.Win32;

namespace FPSBooster.App.Services;

/// <summary>Tous les tweaks REG. Chaque méthode est idempotente et loggée.</summary>
public static class WindowsTweakService
{
    public static bool DisableFSO()
    {
        bool ok = true;
        ok &= RegistryHelper.SetHKCU(@"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2);
        ok &= RegistryHelper.SetHKCU(@"System\GameConfigStore", "GameDVR_HonorUserFSEBehaviorMode", 1);
        ok &= RegistryHelper.SetHKCU(@"System\GameConfigStore", "GameDVR_DXGIHonorFSEWindowsCompatible", 1);
        ok &= RegistryHelper.SetHKCU(@"System\GameConfigStore", "GameDVR_FSEBehavior", 2);
        return ok;
    }

    public static bool DisableGameDVR()
    {
        bool ok = true;
        ok &= RegistryHelper.SetHKCU(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", 0);
        ok &= RegistryHelper.SetHKCU(@"System\GameConfigStore", "GameDVR_Enabled", 0);
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0);
        ok &= RegistryHelper.SetHKCU(@"SOFTWARE\Microsoft\Windows\CurrentVersion\GameDVR", "GameDVR_Enabled", 0);
        return ok;
    }

    public static bool UnlockFpsLimit()
    {
        // Désactive le limiteur implicite : DVR off + FSO off + Game Bar OFF + VSync global off (NVIDIA/AMD via registre générique)
        bool ok = true;
        ok &= DisableGameDVR();
        ok &= RegistryHelper.SetHKCU(@"SOFTWARE\Microsoft\GameBar", "AutoGameModeEnabled", 0);
        ok &= RegistryHelper.SetHKCU(@"SOFTWARE\Microsoft\GameBar", "AllowAutoGameMode", 0);
        // Désactive le throttle DXGI
        ok &= RegistryHelper.SetHKCU(@"System\GameConfigStore", "GameDVR_FSEBehaviorMode", 2);
        return ok;
    }

    public static bool VisualEffectsBestPerformance()
    {
        bool ok = true;
        ok &= RegistryHelper.SetHKCU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects", "VisualFXSetting", 2);
        ok &= RegistryHelper.SetHKCU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarAnimations", 0);
        ok &= RegistryHelper.SetHKCU(@"Control Panel\Desktop", "MenuShowDelay", 0);
        ok &= RegistryHelper.SetHKCU(@"Control Panel\Desktop\WindowMetrics", "MinAnimate", 0);
        ok &= RegistryHelper.SetHKCU(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", "EnableTransparency", 0);
        ok &= RegistryHelper.SetHKCU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "ListviewAlphaSelect", 0);
        return ok;
    }

    public static bool DedicatedGpuPreference()
    {
        bool ok = true;
        // Force le GPU haute perf par défaut + désactive l'éco iGPU pour les jeux
        ok &= RegistryHelper.SetHKCU(@"Software\Microsoft\DirectX\UserGpuPreferences", "DirectXUserGlobalSettings", 0);
        RegistryHelper.Set(RegistryHive.CurrentUser, @"Software\Microsoft\DirectX\UserGpuPreferences",
            "GpuPreference", "2;GpuPreference=2;", RegistryValueKind.String);
        // HAGS ON (Hardware Accelerated GPU Scheduling)
        ok &= RegistryHelper.SetHKLM(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
        return ok;
    }

    public static bool UnparkCpuCores()
    {
        bool ok = true;
        // Affiche l'option + met 100% cœurs actifs
        ok &= RegistryHelper.SetHKLM(@"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583", "Attributes", 0);
        ok &= RegistryHelper.SetHKLM(@"SYSTEM\CurrentControlSet\Control\Power\PowerSettings\54533251-82be-4824-96c1-47b60b740d00\0cc5b647-c1df-4637-891a-dec35c318583", "ValueMax", 100);
        // Désactive le parking via powercfg (heterogeneous + core parking)
        _ = CmdHelper.PowerCfg("-setacvalueindex SCHEME_CURRENT SUB_PROCESSOR CPMINCORES 100");
        _ = CmdHelper.PowerCfg("-setactive SCHEME_CURRENT");
        return ok;
    }

    public static bool FreeUpCpuUsage()
    {
        int killed = ProcessService.KillBloat(includeBrowsers: false);
        CleanerService.TrimMemory();
        RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0);
        Logger.Info($"FreeUpCpu: {killed} process tués");
        return true;
    }

    public static bool MousePrecisionOff()
    {
        bool ok = true;
        // Précision pointeur OFF (essentiel input lag souris)
        ok &= RegistryHelper.SetHKCU(@"Control Panel\Mouse", "MouseSpeed", 0);
        ok &= RegistryHelper.SetHKCU(@"Control Panel\Mouse", "MouseThreshold1", 0);
        ok &= RegistryHelper.SetHKCU(@"Control Panel\Mouse", "MouseThreshold2", 0);
        return ok;
    }

    public static bool KeyboardTuning()
    {
        bool ok = true;
        // Clavier : répétition max
        ok &= RegistryHelper.SetHKCU(@"Control Panel\Keyboard", "KeyboardDelay", 0);
        ok &= RegistryHelper.SetHKCU(@"Control Panel\Keyboard", "KeyboardSpeed", 31);
        return ok;
    }

    public static bool Mouse611()
    {
        // Sensibilité 6/11 (défaut Windows = 1:1, sans accélération logicielle)
        return RegistryHelper.SetHKCUString(@"Control Panel\Mouse", "MouseSensitivity", "10");
    }

    public static bool MarkCMouseFix()
    {
        // Courbe MarkC : pointeur strictement linéaire 1:1 (référence CS/gamers)
        byte[] x = { 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00, 0xC0,0xCC,0x0C,0x00,0x00,0x00,0x00,0x00, 0x80,0x99,0x19,0x00,0x00,0x00,0x00,0x00, 0x40,0x66,0x26,0x00,0x00,0x00,0x00,0x00, 0x00,0x33,0x33,0x00,0x00,0x00,0x00,0x00 };
        byte[] y = { 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00, 0x00,0x00,0x38,0x00,0x00,0x00,0x00,0x00, 0x00,0x00,0x70,0x00,0x00,0x00,0x00,0x00, 0x00,0x00,0xA8,0x00,0x00,0x00,0x00,0x00, 0x00,0x00,0xE0,0x00,0x00,0x00,0x00,0x00 };
        bool ok = true;
        ok &= RegistryHelper.Set(RegistryHive.CurrentUser, @"Control Panel\Mouse", "SmoothMouseXCurve", x, RegistryValueKind.Binary);
        ok &= RegistryHelper.Set(RegistryHive.CurrentUser, @"Control Panel\Mouse", "SmoothMouseYCurve", y, RegistryValueKind.Binary);
        return ok;
    }

    public static bool StickyKeysOff()
    {
        // Coupe les popups d'accessibilité qui interrompent une partie (Shift x5...)
        bool ok = true;
        ok &= RegistryHelper.SetHKCUString(@"Control Panel\Accessibility\StickyKeys", "Flags", "506");
        ok &= RegistryHelper.SetHKCUString(@"Control Panel\Accessibility\ToggleKeys", "Flags", "58");
        ok &= RegistryHelper.SetHKCUString(@"Control Panel\Accessibility\FilterKeys", "Flags", "0");
        return ok;
    }

    public static bool WinKeysOff()
    {
        // Désactive les raccourcis touche Windows (évite de quitter le plein écran par accident)
        return RegistryHelper.SetHKCU(@"Software\Microsoft\Windows\CurrentVersion\Policies\Explorer", "NoWinKeys", 1);
    }

    public static bool ShakeMinimizeOff()
    {
        // Désactive "secouer pour réduire" (minimisation accidentelle en jeu)
        return RegistryHelper.SetHKCU(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisallowShaking", 1);
    }

    public static async Task<bool> DynamicTickAsync()
    {
        // Timer : désactive le tick dynamique (demande reboot, safe)
        var (c1, _, _) = await CmdHelper.Bcdedit("/set disabledynamictick yes");
        var (c2, _, _) = await CmdHelper.Bcdedit("/set tscsyncpolicy Enhanced");
        return c1 == 0 && c2 == 0;
    }

    public static bool MmcssGames()
    {
        // Priorité jeux MMCSS
        bool ok = true;
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 8);
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 6);
        RegistryHelper.Set(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", "High", RegistryValueKind.String);
        return ok;
    }

    public static async Task<bool> FpsInputHighEndAsync()
    {
        bool ok = true;
        ok &= MousePrecisionOff();
        ok &= KeyboardTuning();
        ok &= await DynamicTickAsync();
        ok &= MmcssGames();
        return ok;
    }

    public static bool OptimizeFps()
    {
        bool ok = true;
        // Game Mode ON
        ok &= RegistryHelper.SetHKCU(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
        ok &= RegistryHelper.SetHKCU(@"Software\Microsoft\GameBar", "AllowAutoGameMode", 1);
        // HAGS + GPU priority
        ok &= RegistryHelper.SetHKLM(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 8);
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 6);
        ok &= DisableGameDVR();
        return ok;
    }

    public static bool OptimizeGraphicsCard()
    {
        bool ok = true;
        // Désactive MPO (Multiplane Overlay) source de stutter sur NVIDIA/AMD
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5);
        // HAGS ON
        ok &= RegistryHelper.SetHKLM(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2);
        // Latence faible : désactive le preemption agressif NVIDIA
        ok &= RegistryHelper.SetHKLM(@"SYSTEM\CurrentControlSet\Services\nvlddmkm\FTS", "EnablePreemption", 0);
        // TdrDelay plus tolérant (évite les freezes driver)
        ok &= RegistryHelper.SetHKLM(@"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "TdrDelay", 10);
        return ok;
    }

    public static bool OptimizeAllWindowsSettings()
    {
        bool ok = true;
        // Télémétrie OFF
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0);
        // Historique activité OFF
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0);
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0);
        // Recherche web OFF (plus rapide)
        ok &= RegistryHelper.SetHKCU(@"Software\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1);
        // Transparence + animations OFF
        ok &= VisualEffectsBestPerformance();
        // Game DVR OFF + Game Mode ON
        ok &= DisableGameDVR();
        ok &= RegistryHelper.SetHKCU(@"Software\Microsoft\GameBar", "AutoGameModeEnabled", 1);
        // Désactive les suggestions / pubs Start
        ok &= RegistryHelper.SetHKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SilentInstalledAppsEnabled", 0);
        ok &= RegistryHelper.SetHKCU(@"Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager", "SystemPaneSuggestionsEnabled", 0);
        return ok;
    }

    public static bool CpuPriority()
    {
        bool ok = true;
        // 26 hex = 38 dec : favorise le premier plan (jeux). Valeur réversible.
        ok &= RegistryHelper.SetHKLM(@"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38);
        // IRQ8 priorité haute (horloge système -> input)
        ok &= RegistryHelper.SetHKLM(@"SYSTEM\CurrentControlSet\Control\PriorityControl", "IRQ8Priority", 1);
        return ok;
    }

    public static bool SystemResponsiveness()
    {
        bool ok = true;
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0);
        return ok;
    }

    public static bool GamingDoNotDisturb(bool on)
    {
        // Coupe/rétablit TOUTES les notifications toast Windows (0 = coupées)
        return RegistryHelper.SetHKCU(@"Software\Microsoft\Windows\CurrentVersion\Notifications\Settings", "NOC_GLOBAL_SETTING_TOASTS_ENABLED", on ? 0 : 1);
    }

    public static bool SystemResponsivenessGpu()
    {
        bool ok = true;
        ok &= SystemResponsiveness();
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF));
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "GPU Priority", 8);
        ok &= RegistryHelper.SetHKLM(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Priority", 6);
        RegistryHelper.Set(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games", "Scheduling Category", "High", RegistryValueKind.String);
        return ok;
    }
}
