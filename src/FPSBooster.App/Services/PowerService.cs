namespace FPSBooster.App.Services;

public static class PowerService
{
    public const string UltimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    public const string HighPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";

    public static async Task<bool> EnsureUltimatePlanAsync()
    {
        // Journalise l'ancien plan pour Annuler
        string oldScheme = await GetActiveSchemeAsync();
        // Duplique le plan Ultimate s'il n'existe pas (ignore l'erreur s'il existe déjà)
        await CmdHelper.PowerCfg($"-duplicatescheme {UltimateGuid}");
        var (code, _, _) = await CmdHelper.PowerCfg($"-setactive {UltimateGuid}");
        bool activated = code == 0;
        if (!activated)
        {
            // Fallback : haute performance (vérifié, pas supposé)
            var (fb, _, _) = await CmdHelper.PowerCfg($"-setactive {HighPerfGuid}");
            activated = fb == 0;
            if (activated)
                Logger.Warn("Power plan Ultimate indisponible, repli Haute performance");
        }
        if (activated && oldScheme != "" && !oldScheme.Equals(UltimateGuid, StringComparison.OrdinalIgnoreCase))
            TweakJournal.AppendPowerPlan(oldScheme);
        // Désactive la mise en veille disque / USB selective suspend / PCIe éco
        await CmdHelper.PowerCfg("-change -disk-timeout-ac 0");
        await CmdHelper.PowerCfg("-change -standby-timeout-ac 0");
        await CmdHelper.PowerCfg("-setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0"); // USB off
        // Alias SUB_PCIEXPRESS/ASPM = portable (les GUID changent selon les machines)
        await CmdHelper.PowerCfg("-setacvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 0"); // PCIe max perf (secteur)
        await CmdHelper.PowerCfg("-setdcvalueindex SCHEME_CURRENT SUB_PCIEXPRESS ASPM 0"); // PCIe max perf (batterie)
        await CmdHelper.PowerCfg("-setactive SCHEME_CURRENT");
        if (activated)
        {
            Logger.Info("Power plan: Ultimate Performance activé");
            return true;
        }
        Logger.Warn("Power plan: activation Ultimate/ Haute performance échouée");
        return false;
    }

    public static async Task<string> GetActiveSchemeAsync()
    {
        try
        {
            var (code, stdout, _) = await CmdHelper.PowerCfg("-getactivescheme");
            if (code != 0) return "";
            var m = System.Text.RegularExpressions.Regex.Match(stdout, @"\{[0-9A-Fa-f\-]{36}\}");
            return m.Success ? m.Value.Trim('{', '}') : "";
        }
        catch { return ""; }
    }

    public static async Task DisablePowerThrottlingAsync()
    {
        // Désactive le throttling CPU moderne
        RegistryHelper.SetHKLM(@"SYSTEM\CurrentControlSet\Control\Power\PowerThrottling", "PowerThrottlingOff", 1);
        await CmdHelper.PowerCfg("-attributes SUB_PROCESSOR PERFBOOSTMODE -ATTRIB_HIDE");
        await CmdHelper.PowerCfg("-setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PERFBOOSTMODE 1"); // Aggressive
        await CmdHelper.PowerCfg("-setactive SCHEME_CURRENT");
    }
}
