using System.Diagnostics;

namespace FPSBooster.App.Services;

public static class ProcessService
{
    // Process safe à killer (noms exacts sans .exe). Jamais de process système critique.
    // Note : les noms avec espaces sont valides (ex: "Adobe Desktop Service.exe").
    private static readonly string[] BloatNames =
    {
        "OneDrive", "Teams", "Spotify", "Discord", "Skype",
        "Adobe Desktop Service", "Adobe CEF Helper", "CCXProcess",
        "Cortana", "YourPhone", "PhoneExperienceHost",
        "XboxGameBar", "GameBar", "Widgets", "WidgetService",
        "Chrome", "msedge", "firefox", "Opera", // navigateurs (optionnel, fermés pour libérer RAM)
    };

    // Toujours protégés
    private static readonly HashSet<string> Protected = new(StringComparer.OrdinalIgnoreCase)
    {
        "System", "Registry", "smss", "csrss", "wininit", "services", "lsass",
        "winlogon", "explorer", "dwm", "sihost", "taskhostw", "svchost",
        "TokkyoOptimizerClone", "R33333ADOptimizer", "FPSBooster", "devenv", "msbuild"
    };

    public static int KillBloat(bool includeBrowsers = false)
    {
        int killed = 0;
        foreach (var name in BloatNames)
        {
            if (!includeBrowsers && (name is "Chrome" or "msedge" or "firefox" or "Opera"))
                continue;
            if (Protected.Contains(name)) continue;
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    string pname;
                    try { pname = p.ProcessName; }
                    catch (Exception ex)
                    {
                        Logger.Warn($"KillBloat lecture nom ('{name}'): {ex.Message}");
                        p.Dispose();
                        continue;
                    }
                    try
                    {
                        if (Protected.Contains(pname)) continue;
                        p.Kill();
                        killed++;
                        Logger.Info($"Kill {pname} ({p.Id})");
                    }
                    catch (Exception ex) { Logger.Warn($"Kill '{pname}' impossible: {ex.Message}"); }
                    finally { p.Dispose(); }
                }
            }
            catch (Exception ex) { Logger.Warn($"KillBloat '{name}': {ex.Message}"); }
        }
        return killed;
    }

    public static int SetGamePriority(string[] gameProcessNames)
    {
        int changed = 0;
        foreach (var name in gameProcessNames)
        {
            try
            {
                foreach (var p in Process.GetProcessesByName(name))
                {
                    string pname;
                    try { pname = p.ProcessName; }
                    catch (Exception ex)
                    {
                        Logger.Warn($"SetGamePriority lecture nom ('{name}'): {ex.Message}");
                        p.Dispose();
                        continue;
                    }
                    try
                    {
                        p.PriorityClass = ProcessPriorityClass.High;
                        changed++;
                    }
                    catch (Exception ex) { Logger.Warn($"Priorité '{pname}' impossible: {ex.Message}"); }
                    finally { p.Dispose(); }
                }
            }
            catch (Exception ex) { Logger.Warn($"SetGamePriority '{name}': {ex.Message}"); }
        }
        return changed;
    }

    public static string[] DetectRunningGames()
    {
        string[] known = { "FiveM_GTAProcess", "GTA5", "FortniteClient-Win64-Shipping", "VALORANT-Win64-Shipping", "cod", "cs2", "Minecraft", "RobloxPlayerBeta" };
        var found = new List<string>();
        foreach (var n in known)
        {
            try { if (Process.GetProcessesByName(n).Length > 0) found.Add(n); }
            catch (Exception ex) { Logger.Warn($"DetectRunningGames '{n}': {ex.Message}"); }
        }
        return found.ToArray();
    }
}
