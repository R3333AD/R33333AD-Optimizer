namespace FPSBooster.App.Services;

/// <summary>Active/désactive les services Windows lourds (réversible, loggé).</summary>
public static class SystemServices
{
    // service -> démarrage par défaut Windows (pour restauration)
    private static readonly Dictionary<string, string> Defaults = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SysMain"] = "auto",
        ["DiagTrack"] = "auto",
        ["WSearch"] = "auto",
        ["XblGameSave"] = "demand",
        ["XboxGipSvc"] = "demand",
        ["XboxNetApiSvc"] = "demand",
        ["XblAuthManager"] = "demand",
        ["Spooler"] = "auto",
        ["Fax"] = "demand",
        ["MapsBroker"] = "auto",
        ["lfsvc"] = "demand",
        ["SharedAccess"] = "demand",
        ["RemoteRegistry"] = "disabled",
        ["RetailDemo"] = "demand",
        ["wisvc"] = "demand",
        ["WMPNetworkSvc"] = "demand",
        ["WerSvc"] = "demand",
        ["DPS"] = "auto",
        ["TrkWks"] = "auto",
        ["bthserv"] = "demand",
        ["BTAGService"] = "demand",
        ["SSDPSRV"] = "demand",
        ["upnphost"] = "demand",
        ["fdPHost"] = "demand",
        ["FDResPub"] = "demand",
        ["iphlpsvc"] = "auto",
        ["RasMan"] = "demand",
        ["LanmanServer"] = "auto",
        ["TabletInputService"] = "demand",
        ["WbioSrvc"] = "demand",
        ["PcaSvc"] = "demand",
        ["ShellHWDetection"] = "auto",
        ["stisvc"] = "demand",
        ["DoSvc"] = "auto",
    };

    public static async Task<bool> SetStartAsync(string service, string mode)
    {
        // mode: auto | demand | disabled — journalise l'ancien mode pour Annuler
        string oldMode = await GetStartModeAsync(service);
        var (code, _, _) = await CmdHelper.RunAsync("sc.exe", $"config {service} start= {mode}");
        if (code == 0 && oldMode != "" && !oldMode.Equals(mode, StringComparison.OrdinalIgnoreCase))
            TweakJournal.AppendService(service, oldMode);
        Logger.Info($"Service {service} start= {mode} (avant: {oldMode}) exit={code}");
        return code == 0;
    }

    public static async Task<string> GetStartModeAsync(string service)
    {
        try
        {
            var (code, stdout, _) = await CmdHelper.RunAsync("sc.exe", $"qc {service}", 10_000);
            if (code != 0) return "";
            var m = System.Text.RegularExpressions.Regex.Match(stdout, @"START_TYPE\s*:\s*\d+\s+(\w+)");
            if (!m.Success) return "";
            return m.Groups[1].Value switch
            {
                "AUTO_START" => "auto",
                "DEMAND_START" => "demand",
                "DISABLED" => "disabled",
                "BOOT_START" => "boot",
                "SYSTEM_START" => "system",
                var other => other.ToLowerInvariant(),
            };
        }
        catch { return ""; }
    }

    public static async Task<bool> SysMainDemandAsync() => await SetStartAsync("SysMain", "demand");
    public static async Task<bool> DisableDiagTrackAsync() => await SetStartAsync("DiagTrack", "disabled");
    public static async Task<bool> SearchDemandAsync() => await SetStartAsync("WSearch", "demand");

    public static async Task<bool> DisableXboxServicesAsync()
    {
        bool ok = true;
        foreach (var svc in new[] { "XblGameSave", "XboxGipSvc", "XboxNetApiSvc" })
            ok &= await SetStartAsync(svc, "demand");
        return ok;
    }

    public static async Task<bool> RestoreDefaultsAsync()
    {
        bool ok = true;
        foreach (var (svc, mode) in Defaults)
            ok &= await SetStartAsync(svc, mode);
        return ok;
    }
}
