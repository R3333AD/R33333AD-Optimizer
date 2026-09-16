using System.Text.RegularExpressions;
using FPSBooster.App.Models;

namespace FPSBooster.App.Services;

/// <summary>Mappe les cartes UI -> vraies actions. Sections: home, clean, fps, input, kbd, ram, reg, svc, tools, uninst, net, profils.</summary>
public static partial class TweakLibrary
{
    public record Def(string ActionId, string Title, string Badge, string[] Sections, Func<Task<TweakResult>> Run);

    public static readonly List<Def> All = new()
    {
        // ---------- FPS & Performance ----------
        new("boost1", "Boost FPS 1", "CMD", new[]{"fps"}, BoostService.Boost1Async),
        new("boost3", "Boost FPS 3", "CMD", new[]{"fps"}, BoostService.Boost3Async),
        new("boost_fivem", "Boost FPS for FiveM", "CMD", new[]{"fps","clean"}, BoostService.BoostFiveMAsync),

        new("disable_fso", "Disable FSO (Fullscreen Optimizations)", "REG", new[]{"fps","reg"},
            () => Task.FromResult(WindowsTweakService.DisableFSO() ? Ok("FSO désactivé") : Fail("FSO"))),
        new("disable_dvr", "Disable Game DVR", "REG", new[]{"fps","reg"},
            () => Task.FromResult(WindowsTweakService.DisableGameDVR() ? Ok("Game DVR désactivé") : Fail("DVR"))),
        new("unlock_fps", "Unlock FPS Limit", "REG", new[]{"fps","reg"},
            () => Task.FromResult(WindowsTweakService.UnlockFpsLimit() ? Ok("Limite FPS déverrouillée") : Fail("Unlock"))),
        new("visual_fx", "Visual Effects", "REG", new[]{"fps","reg"},
            () => Task.FromResult(WindowsTweakService.VisualEffectsBestPerformance() ? Ok("Effets visuels au minimum") : Fail("Visuels"))),
        new("dgpu", "Dedicated GPU (Preference)", "REG", new[]{"fps","reg"},
            () => Task.FromResult(WindowsTweakService.DedicatedGpuPreference() ? Ok("GPU dédié + HAGS ON") : Fail("GPU"))),
        new("unpark_cpu", "Unpark CPU Cores", "REG", new[]{"fps","reg"},
            () => Task.FromResult(WindowsTweakService.UnparkCpuCores() ? Ok("Cœurs CPU déparqués (100%)") : Fail("CPU"))),
        new("free_cpu", "Free Up CPU Usage", "REG", new[]{"fps","ram"},
            () => Task.FromResult(WindowsTweakService.FreeUpCpuUsage() ? Ok("CPU libéré") : Fail("CPU"))),
        new("fps_input_highend", "FPS & Input Optimization (High-End)", "REG", new[]{"fps","input"},
            async () => await WindowsTweakService.FpsInputHighEndAsync() ? Ok("Input lag réduit (souris/clavier/timer)") : Fail("Input")),
        new("power_opt", "Power Optimizations", "REG", new[]{"fps"},
            async () => { await PowerService.EnsureUltimatePlanAsync(); await PowerService.DisablePowerThrottlingAsync(); return Ok("Plan Ultimate + anti-throttle"); }),
        new("opt_fps", "Optimize FPS", "REG", new[]{"fps"},
            () => Task.FromResult(WindowsTweakService.OptimizeFps() ? Ok("FPS optimisé (GameMode+HAGS)") : Fail("FPS"))),
        new("fps_measure", "Measure FPS (15s)", "CMD", new[]{"fps","tools"},
            async () =>
            {
                var (ok, msg) = await FpsMeasureService.MeasureAsync();
                return new TweakResult(ok, msg);
            }),
        new("profil_valorant", "⚡ Profil Valorant", "GAME", new[]{"profils"},
            async () => await RunProfileAsync("profil_valorant")),
        new("profil_fortnite", "⚡ Profil Fortnite", "GAME", new[]{"profils"},
            async () => await RunProfileAsync("profil_fortnite")),
        new("profil_warzone", "⚡ Profil Warzone", "GAME", new[]{"profils"},
            async () => await RunProfileAsync("profil_warzone")),
        new("profil_fivem", "⚡ Profil FiveM", "GAME", new[]{"profils"},
            async () => await RunProfileAsync("profil_fivem")),
        new("session_start", "Start Gaming Session", "CMD", new[]{"fps"},
            async () => await SessionService.StartAsync()),
        new("session_stop", "Stop Gaming Session", "CMD", new[]{"fps"},
            async () => await SessionService.StopAsync()),
        new("gpu_game", "GPU High-Perf for Running Game", "REG", new[]{"fps"},
            () => Task.FromResult(SessionService.GpuPreferenceForRunningGames())),
        new("opt_entire", "Optimize Entire PC", "REG", new[]{"fps"},
            async () => {
                await BoostService.Boost1Async();
                WindowsTweakService.OptimizeAllWindowsSettings();
                WindowsTweakService.OptimizeGraphicsCard();
                await PowerService.EnsureUltimatePlanAsync();
                await NetworkService.OptimizeTcpAsync();
                return Ok("PC entièrement optimisé");
            }),
        new("opt_powerplan", "Optimize Power Plan", "REG", new[]{"fps","tools"},
            async () => { await PowerService.EnsureUltimatePlanAsync(); return Ok("Power Plan Ultimate actif"); }),
        new("opt_gpu", "Optimize Graphics Card", "REG", new[]{"fps","reg"},
            () => Task.FromResult(WindowsTweakService.OptimizeGraphicsCard() ? Ok("GPU optimisé (MPO off, TdrDelay)") : Fail("GPU"))),
        new("opt_windows_all", "Optimize ALL Windows Settings", "REG", new[]{"fps","reg"},
            () => Task.FromResult(WindowsTweakService.OptimizeAllWindowsSettings() ? Ok("Windows allégé (télémétrie off)") : Fail("Windows"))),
        new("cpu_priority", "CPU Priority", "REG", new[]{"fps","reg"},
            () => Task.FromResult(WindowsTweakService.CpuPriority() ? Ok("Priorité CPU jeux (38)") : Fail("CPU prio"))),
        new("sysresp_gpu", "System Responsiveness + GPU", "REG", new[]{"fps","reg"},
            () => Task.FromResult(WindowsTweakService.SystemResponsivenessGpu() ? Ok("SystemResponsiveness=0 + GPU High") : Fail("SysResp"))),
        new("sysresp", "System Responsiveness", "REG", new[]{"fps","reg"},
            () => Task.FromResult(WindowsTweakService.SystemResponsiveness() ? Ok("SystemResponsiveness=0") : Fail("SysResp"))),

        // ---------- Cleanup ----------
        new("clean_temp", "Clean Temp Files", "CMD", new[]{"clean","ram"},
            async () => { int n = await CleanerService.CleanTempAsync(); return Ok($"{n} fichiers temp supprimés"); }),
        new("clear_do", "Clear Delivery Optimization", "CMD", new[]{"clean"},
            async () => { await CleanerService.ClearDeliveryOptimizationAsync(); return Ok("Cache Delivery Optimization vidé"); }),
        new("flush_dns", "Flush DNS", "CMD", new[]{"clean","net"},
            async () => { await NetworkService.FlushDnsAsync(); return Ok("DNS flushé"); }),
        new("empty_recycle", "Empty Recycle Bin", "CMD", new[]{"clean"},
            async () => Ok(await CleanerService.ClearRecycleBinAsync())),
        new("clean_fivem", "Clean FiveM Cache", "CMD", new[]{"clean"},
            () => Task.FromResult(Ok($"Cache FiveM vidé ({CleanerService.CleanFiveMCache()} entrées)"))),
        new("clean_logs", "Clean Log Files", "CMD", new[]{"clean"},
            async () => { var (f, b) = await CleanerService.CleanLogsAsync(); return Ok($"{f} logs supprimés ({FormatLogSize(b)} libérés)"); }),
        new("clean_browsers", "Clean Browser Caches", "CMD", new[]{"clean"},
            async () => { var (f, b) = await CleanerService.CleanBrowsersAsync(); return Ok($"{f} fichiers cache ({FormatLogSize(b)}) — historique/mdp intacts (ferme navigateurs pour 100%)"); }),

        // ---------- Input Lag & Latency / Keyboard & Mouse ----------
        new("mouse_fix", "Mouse Precision OFF", "REG", new[]{"input","kbd"},
            () => Task.FromResult(WindowsTweakService.MousePrecisionOff() ? Ok("Précision pointeur OFF (1:1)") : Fail("Souris"))),
        new("kbd_tune", "Keyboard Tuning", "REG", new[]{"input","kbd"},
            () => Task.FromResult(WindowsTweakService.KeyboardTuning() ? Ok("Clavier : répétition max") : Fail("Clavier"))),
        new("mouse_611", "Mouse 6/11 (1:1)", "REG", new[]{"kbd"},
            () => Task.FromResult(WindowsTweakService.Mouse611() ? Ok("Souris 6/11 (défaut 1:1)") : Fail("Souris"))),
        new("markc", "MarkC Mouse Fix", "REG", new[]{"kbd"},
            () => Task.FromResult(WindowsTweakService.MarkCMouseFix() ? Ok("Courbe MarkC linéaire 1:1") : Fail("MarkC"))),
        new("sticky_off", "Disable Sticky Keys", "REG", new[]{"kbd","input"},
            () => Task.FromResult(WindowsTweakService.StickyKeysOff() ? Ok("Touches rémanentes OFF (plus de popup en jeu)") : Fail("Sticky"))),
        new("winkeys_off", "Disable Win-Key Hotkeys", "REG", new[]{"kbd"},
            () => Task.FromResult(WindowsTweakService.WinKeysOff() ? Ok("Raccourcis Win OFF (plein écran protégé)") : Fail("WinKeys"))),
        new("shake_off", "Disable Shake Minimize", "REG", new[]{"kbd"},
            () => Task.FromResult(WindowsTweakService.ShakeMinimizeOff() ? Ok("Secouer-pour-réduire OFF") : Fail("Shake"))),
        new("dyn_tick", "Disable Dynamic Tick", "CMD", new[]{"input"},
            async () => await WindowsTweakService.DynamicTickAsync() ? Ok("Dynamic tick OFF (reboot requis)") : Fail("Tick")),
        new("mmcss", "MMCSS Games Priority", "REG", new[]{"input"},
            () => Task.FromResult(WindowsTweakService.MmcssGames() ? Ok("Priorité MMCSS jeux (GPU 8 / CPU 6)") : Fail("MMCSS"))),
        new("nagle", "Disable Nagle", "REG", new[]{"input","net"},
            () => Task.FromResult(RunNagle())),
        new("tcp_opt", "Optimize TCP (Gaming)", "REG", new[]{"input","net"},
            async () => { await NetworkService.OptimizeTcpAsync(); return Ok("TCP gaming (QoS 0%, autotuning)"); }),

        // ---------- RAM ----------
        new("trim_ram", "Trim RAM (Working Sets)", "CMD", new[]{"ram"},
            () => Task.FromResult(Ok($"{CleanerService.TrimMemory()} process trimmés (RAM libérée)"))),

        // ---------- Services ----------
        new("svc_sysmain", "SysMain → Manual", "CMD", new[]{"svc","ram"},
            async () => await SystemServices.SysMainDemandAsync() ? Ok("SysMain en manuel (moins de disque)") : Fail("SysMain")),
        new("svc_diagtrack", "Disable DiagTrack", "CMD", new[]{"svc"},
            async () => await SystemServices.DisableDiagTrackAsync() ? Ok("Télémétrie service désactivé") : Fail("DiagTrack")),
        new("svc_wsearch", "Windows Search → Manual", "CMD", new[]{"svc"},
            async () => await SystemServices.SearchDemandAsync() ? Ok("Windows Search en manuel") : Fail("WSearch")),
        new("svc_xbox", "Xbox Services → Manual", "CMD", new[]{"svc"},
            async () => await SystemServices.DisableXboxServicesAsync() ? Ok("Services Xbox en manuel") : Fail("Xbox")),
        new("svc_restore", "Restore Services Defaults", "CMD", new[]{"svc"},
            async () => await SystemServices.RestoreDefaultsAsync() ? Ok("Services restaurés par défaut") : Fail("Services")),

        // ---------- Programs & Tools ----------
        new("gaming_dns", "Gaming DNS (1.1.1.1)", "CMD", new[]{"tools","net"},
            async () => { await NetworkService.SetGamingDnsAsync(); return Ok("DNS gaming 1.1.1.1 / 8.8.8.8"); }),
        new("ping_test", "Ping Test (1.1.1.1)", "CMD", new[]{"tools","net"},
            async () => Ok(SummarizePing(await NetworkService.PingTestAsync()))),
        new("restore_point", "Create Restore Point", "CMD", new[]{"tools"},
            () => { AdminHelper.CreateRestorePoint("FPSBooster - manuel"); return Task.FromResult(Ok("Point de restauration créé")); }),
        new("benchmark", "PC Benchmark", "CMD", new[]{"tools"},
            async () => Ok((await BenchmarkService.RunAsync()).Summary)),
        new("view_logs", "View / Export Logs", "CMD", new[]{"tools"},
            () =>
            {
                try
                {
                    new Views.LogWindow().Show();
                    return Task.FromResult(Ok("Visionneuse de logs ouverte"));
                }
                catch (Exception ex) { return Task.FromResult(new TweakResult(false, ex.Message)); }
            }),
        new("check_updates", "Check for Updates", "CMD", new[]{"tools"},
            async () =>
            {
                var (found, msg) = await UpdateService.CheckAsync();
                return new TweakResult(true, msg);
            }),
        new("export_report", "Exporter le rapport (.md)", "CMD", new[]{"tools"},
            () => Task.FromResult(ReportService.Export())),
        new("export_config", "Exporter la config (.zip)", "CMD", new[]{"tools"},
            () => Task.Run(() => ConfigBackupService.Export())),
        new("import_config", "Importer une config (.zip)", "CMD", new[]{"tools"},
            () => Task.FromResult(ConfigBackupService.ImportWithDialog())),
        new("self_uninstall", "🗑 Uninstall R33333AD (zéro trace)", "CMD", new[]{"tools"},
            () =>
            {
                var confirm = System.Windows.MessageBox.Show(
                    Loc.Instance.Get("SelfUn_Msg"),
                    Loc.Instance.Get("SelfUn_Title"),
                    System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Warning);
                if (confirm != System.Windows.MessageBoxResult.Yes)
                    return Task.FromResult(new TweakResult(false, Loc.Instance.Get("SelfUn_Cancel")));
                var (files, keys) = UninstallerService.SelfUninstall();
                Logger.Info("Self-uninstall exécuté, fermeture de l'app");
                System.Windows.Application.Current.Dispatcher.InvokeAsync(
                    () => System.Windows.Application.Current.Shutdown());
                return Task.FromResult(new TweakResult(true, Loc.Instance.Get("SelfUn_Done", files, keys)));
            }),
        new("startup_bloat", "Remove Startup Bloat", "REG", new[]{"tools"},
            () => Task.FromResult(Ok($"{StartupService.RemoveBloatStartup()} entrées démarrage supprimées"))),

        // ---------- Network (package dédié : 100% réseau, zéro modif système) ----------
        new("jitter", "Jitter + Loss Test", "CMD", new[]{"net"},
            async () => Ok(await NetworkService.JitterTestAsync())),
        new("net_info", "Network Diagnostics", "CMD", new[]{"net"},
            async () => Ok(await NetworkService.NetworkInfoAsync())),
        new("arp_flush", "Flush ARP Cache", "CMD", new[]{"net"},
            async () => { await NetworkService.FlushArpAsync(); return Ok("Cache ARP + NetBIOS vidé"); }),
        new("tunnels_off", "Disable Teredo/6to4/ISATAP", "CMD", new[]{"net"},
            async () => { await NetworkService.DisableTunnelsAsync(); return Ok("Tunnels désactivés (moins d'overhead)"); }),
        new("do_nop2p", "Delivery Opt. No P2P", "REG", new[]{"net"},
            () => Task.FromResult(RunDoNoP2P())),
        new("net_reset", "Reset TCP/IP + Winsock", "CMD", new[]{"net"},
            async () => { await NetworkService.ResetStackAsync(); return Ok("Stack réinitialisée (REBOOT requis)"); }),
        new("net_restore", "Restore Network Defaults", "CMD", new[]{"net"},
            async () => { await NetworkService.RestoreNetworkDefaultsAsync(); return Ok("Réseau restauré par défaut"); }),
    };

    private static async Task<TweakResult> RunProfileAsync(string profileId)
    {
        var profile = GameProfiles.ById(profileId);
        if (profile == null) return new(false, "Profil inconnu");
        // Mesure FPS avant (silencieuse si jeu absent ou PresentMon manquant)
        var (okB, fpsB, _, _, _, _) = await FpsMeasureService.TryMeasureAverageFpsAsync(10);
        var (succeeded, fail, prio) = await GameProfiles.ExecuteAsync(profile);
        // Mémorise les réussites pour allumer les LEDs (via le journal d'exécution)
        LastProfileSucceeded = succeeded;
        // Mesure après + gain enregistré sous l'id du profil (relie LEDs et mesuré)
        string extra = "";
        if (okB)
        {
            var (okA, fpsA, _, _, _, _) = await FpsMeasureService.TryMeasureAverageFpsAsync(10);
            if (okA)
            {
                double gain = FpsGainService.ComputeGainPct(fpsB, fpsA);
                FpsGainService.SaveGain(profileId, fpsB, fpsA, gain);
                extra = $" • {FpsGainService.FormatMeasured(gain)}";
            }
        }
        return succeeded.Count > 0
            ? new(true, $"{profile.Title} : {succeeded.Count} OK / {fail} échecs, priorité jeu x{prio}{extra}")
            : new(false, $"{profile.Title} échoué");
    }

    public static List<string> LastProfileSucceeded { get; private set; } = new();

    // Packs qui exécutent d'autres outils en interne (pour allumer leurs LEDs comme les profils).
    // Uniquement les correspondances quasi complètes (pas les recouvrements partiels).
    private static readonly Dictionary<string, string[]> IncludedTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ["opt_entire"] = new[] { "boost1", "opt_windows_all", "opt_gpu", "opt_powerplan", "tcp_opt" },
        ["boost3"] = new[] { "boost1", "visual_fx", "disable_dvr", "sysresp_gpu", "nagle", "tcp_opt", "svc_sysmain", "svc_diagtrack" },
        ["boost_fivem"] = new[] { "clean_fivem", "boost1", "disable_fso", "opt_gpu", "flush_dns" },
    };

    public static IEnumerable<string> IncludedOf(string actionId) =>
        IncludedTools.TryGetValue(actionId, out var subs) ? subs : Enumerable.Empty<string>();

    private static TweakResult RunNagle()
    {
        try { NetworkService.DisableNagle(); return Ok("Nagle OFF (TcpAckFrequency=1)"); }
        catch (Exception ex) { return new(false, "Nagle échoué : " + ex.Message); }
    }

    private static TweakResult RunDoNoP2P()
    {
        try { NetworkService.DeliveryNoP2P(); return Ok("P2P Windows Update coupé (bande passante préservée)"); }
        catch (Exception ex) { return new(false, "NoP2P échoué : " + ex.Message); }
    }

    private static string SummarizePing(string output)
    {
        var m = PingAverageRegex().Match(output);
        if (m.Success) return $"Ping 1.1.1.1 : moyenne {m.Groups[1].Value}";
        if (output.Contains("échoué")) return output;
        return output.Length > 120 ? output[..120] + "..." : output;
    }

    [GeneratedRegex(@"Moyenne\s*=\s*(\d+\s*ms)|Average\s*=\s*(\d+\s*ms)")]
    private static partial Regex PingAverageRegex();

    private static TweakResult Ok(string m) => new(true, m);
    private static TweakResult Fail(string m) => new(false, m + " échoué (voir logs, lance en admin)");

    // id, titre, services, mode, message OK
    private static readonly (string Id, string Title, string[] Svcs, string Mode, string Ok)[] ServiceTable =
    {
        ("svc_spooler", "Print Spooler → Disabled", new[]{"Spooler"}, "disabled", "Spouleur désactivé (remets en auto si imprimante)"),
        ("svc_fax", "Fax → Disabled", new[]{"Fax"}, "disabled", "Fax désactivé"),
        ("svc_maps", "Downloaded Maps → Disabled", new[]{"MapsBroker"}, "disabled", "Cartes hors ligne désactivées"),
        ("svc_geo", "Geolocation → Manual", new[]{"lfsvc"}, "demand", "Géolocalisation en manuel"),
        ("svc_ics", "Connection Sharing → Disabled", new[]{"SharedAccess"}, "disabled", "Partage de connexion désactivé"),
        ("svc_remotereg", "Remote Registry → Disabled", new[]{"RemoteRegistry"}, "disabled", "Registre distant désactivé (sécurité+)"),
        ("svc_retail", "Retail Demo → Disabled", new[]{"RetailDemo"}, "disabled", "Mode démo désactivé"),
        ("svc_insider", "Insider Service → Disabled", new[]{"wisvc"}, "disabled", "Programme Insider désactivé"),
        ("svc_wmp", "WMP Sharing → Disabled", new[]{"WMPNetworkSvc"}, "disabled", "Partage WMP désactivé"),
        ("svc_wer", "Error Reporting → Disabled", new[]{"WerSvc"}, "disabled", "Rapports d'erreur désactivés"),
        ("svc_dps", "Diag Policy → Disabled", new[]{"DPS"}, "disabled", "Diagnostics auto désactivés"),
        ("svc_trkwks", "Link Tracking → Disabled", new[]{"TrkWks"}, "disabled", "Suivi de liens désactivé"),
        ("svc_bth", "Bluetooth Support → Manual", new[]{"bthserv"}, "demand", "Bluetooth en manuel"),
        ("svc_btag", "Bluetooth Audio → Manual", new[]{"BTAGService"}, "demand", "Audio Bluetooth en manuel"),
        ("svc_ssdp", "SSDP Discovery → Manual", new[]{"SSDPSRV"}, "demand", "SSDP en manuel"),
        ("svc_upnp", "UPnP Host → Manual", new[]{"upnphost"}, "demand", "UPnP en manuel"),
        ("svc_fdp", "Function Discovery → Manual", new[]{"fdPHost", "FDResPub"}, "demand", "Function Discovery en manuel"),
        ("svc_iphlp", "IP Helper → Manual", new[]{"iphlpsvc"}, "demand", "IP Helper en manuel"),
        ("svc_ras", "Remote Access (VPN) → Manual", new[]{"RasMan"}, "demand", "Accès distant en manuel (VPN OK)"),
        ("svc_server", "File Sharing SMB → Manual", new[]{"LanmanServer"}, "demand", "Partage fichiers en manuel"),
        ("svc_tablet", "Touch Keyboard → Manual", new[]{"TabletInputService"}, "demand", "Clavier tactile en manuel"),
        ("svc_bio", "Biometrics → Manual", new[]{"WbioSrvc"}, "demand", "Biométrie en manuel"),
        ("svc_pca", "Compat Assistant → Manual", new[]{"PcaSvc"}, "demand", "Assistant compatibilité en manuel"),
        ("svc_shellhw", "AutoPlay → Manual", new[]{"ShellHWDetection"}, "demand", "AutoPlay en manuel"),
        ("svc_sti", "Scanner/Camera → Manual", new[]{"stisvc"}, "demand", "Acquisition images en manuel"),
        ("svc_do", "Delivery Optim. → Manual", new[]{"DoSvc"}, "demand", "Delivery Optimization en manuel"),
        ("svc_xblauth", "Xbox Auth → Manual", new[]{"XblAuthManager"}, "demand", "Xbox Auth en manuel"),
    };

    static TweakLibrary()
    {
        foreach (var (id, title, svcs, mode, ok) in ServiceTable)
        {
            string[] s = svcs;
            string m = mode, msg = ok, t = title;
            All.Add(new(id, t, "CMD", new[] { "svc" }, async () =>
            {
                bool allOk = true;
                foreach (var svc in s) allOk &= await SystemServices.SetStartAsync(svc, m);
                return allOk ? Ok(msg) : Fail(t);
            }));
        }
    }

    private static string FormatLogSize(long bytes) =>
        bytes >= 1024L * 1024 * 1024 ? $"{bytes / (1024.0 * 1024 * 1024):0.0} Go" :
        bytes >= 1024 * 1024 ? $"{bytes / (1024.0 * 1024):0.0} Mo" :
        bytes >= 1024 ? $"{bytes / 1024.0:0} Ko" : $"{bytes} o";

    public static Def? ById(string id) => All.FirstOrDefault(d => d.ActionId == id);

    // Niveau de risque par outil (défaut = 1 Normal). 0=Léger (sans risque), 2=Expert (risques connus).
    private static readonly Dictionary<string, int> RiskTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["clean_temp"] = 0, ["clear_do"] = 0, ["flush_dns"] = 0, ["empty_recycle"] = 0,
        ["clean_fivem"] = 0, ["clean_logs"] = 0, ["clean_browsers"] = 0, ["trim_ram"] = 0,
        ["ping_test"] = 0, ["jitter"] = 0, ["net_info"] = 0, ["benchmark"] = 0,
        ["restore_point"] = 0, ["mouse_fix"] = 0, ["kbd_tune"] = 0, ["mouse_611"] = 0,
        ["visual_fx"] = 0, ["shake_off"] = 0, ["fps_measure"] = 0, ["svc_restore"] = 0,
        ["boost3"] = 2, ["winkeys_off"] = 2, ["startup_bloat"] = 2,
        ["svc_spooler"] = 2, ["svc_fax"] = 2, ["svc_maps"] = 2, ["svc_ics"] = 2,
        ["svc_remotereg"] = 2, ["svc_retail"] = 2, ["svc_insider"] = 2, ["svc_wmp"] = 2,
        ["svc_wer"] = 2, ["svc_dps"] = 2, ["svc_trkwks"] = 2, ["svc_diagtrack"] = 2,
    };

    public static int RiskOf(string actionId) =>
        RiskTable.TryGetValue(actionId, out int r) ? r : 1;

    // Gain FPS estimé par outil (indicatif : varie selon PC/jeu, non additif).
    // "~0 %" = pas de gain FPS direct (espace/stabilité/sécurité), "—" = non mesurable, "−lag"/"ping −" = latence.
    private static readonly Dictionary<string, (string Text, double Sort)> GainTable = new(StringComparer.OrdinalIgnoreCase)
    {
        ["boost1"] = ("+3–8 %", 5.5), ["boost3"] = ("+5–12 %", 8.5), ["boost_fivem"] = ("+5–15 %", 10),
        ["disable_fso"] = ("+1–3 %", 2), ["disable_dvr"] = ("+2–5 %", 3.5), ["unlock_fps"] = ("+0–2 %", 1),
        ["visual_fx"] = ("+1–3 %", 2), ["dgpu"] = ("+5–20 %", 12.5), ["unpark_cpu"] = ("+0–2 %", 1),
        ["free_cpu"] = ("+1–3 %", 2), ["fps_input_highend"] = ("−lag", 0.5), ["power_opt"] = ("+1–3 %", 2),
        ["opt_fps"] = ("+2–6 %", 4), ["fps_measure"] = ("—", -1),
        ["profil_valorant"] = ("+5–15 %", 10), ["profil_fortnite"] = ("+5–15 %", 10),
        ["profil_warzone"] = ("+5–15 %", 10), ["profil_fivem"] = ("+5–15 %", 10),
        ["session_start"] = ("−lag", 0.5), ["session_stop"] = ("−lag", 0.5),
        ["gpu_game"] = ("+2–10 %", 6), ["opt_entire"] = ("+5–15 %", 10),
        ["opt_powerplan"] = ("+1–3 %", 2), ["opt_gpu"] = ("+1–4 %", 2.5),
        ["opt_windows_all"] = ("+1–3 %", 2), ["cpu_priority"] = ("+0–2 %", 1),
        ["sysresp_gpu"] = ("+0–1 %", 0.5), ["sysresp"] = ("+0–1 %", 0.5),
        ["mouse_fix"] = ("−lag", 0.5), ["kbd_tune"] = ("−lag", 0.5), ["mouse_611"] = ("−lag", 0.5),
        ["markc"] = ("−lag", 0.5), ["sticky_off"] = ("−lag", 0.5), ["winkeys_off"] = ("−lag", 0.5),
        ["shake_off"] = ("−lag", 0.5), ["dyn_tick"] = ("+0–1 %", 0.5), ["mmcss"] = ("+0–2 %", 1),
        ["nagle"] = ("ping −", 0.5), ["tcp_opt"] = ("+0–1 %", 0.5),
        ["trim_ram"] = ("~0 %", 0),
        ["svc_sysmain"] = ("+0–1 %", 0.5), ["svc_diagtrack"] = ("+0–1 %", 0.5),
        ["svc_wsearch"] = ("+0–1 %", 0.5), ["svc_xbox"] = ("+0–1 %", 0.5),
        ["gaming_dns"] = ("ping −", 0.5), ["ping_test"] = ("—", -1),
        ["restore_point"] = ("—", -1), ["benchmark"] = ("—", -1), ["view_logs"] = ("—", -1),
        ["check_updates"] = ("—", -1), ["startup_bloat"] = ("+0–1 %", 0.5),
        ["jitter"] = ("—", -1), ["net_info"] = ("—", -1),
        ["arp_flush"] = ("~0 %", 0), ["tunnels_off"] = ("~0 %", 0),
        ["do_nop2p"] = ("+0–1 %", 0.5), ["net_reset"] = ("~0 %", 0), ["net_restore"] = ("~0 %", 0),
    };

    public static string GainTextOf(string actionId) =>
        GainTable.TryGetValue(actionId, out var g) ? g.Text : "~0 %";

    public static double GainSortOf(string actionId) =>
        GainTable.TryGetValue(actionId, out var g) ? g.Sort : 0;
}
