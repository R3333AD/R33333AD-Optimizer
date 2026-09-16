using FPSBooster.App.Models;

namespace FPSBooster.App.Services;

public record GameProfile(string Id, string Title, string[] Processes, string[] Actions);

/// <summary>Presets 1-clic par jeu : enchaînent nos outils existants + priorité process.</summary>
public static class GameProfiles
{
    public static readonly List<GameProfile> All = new()
    {
        new("profil_valorant", "Profil Valorant", new[]{"VALORANT-Win64-Shipping"},
            new[]{"clean_temp", "mouse_fix", "kbd_tune", "disable_fso", "disable_dvr", "unlock_fps",
                  "dgpu", "opt_fps", "mmcss", "nagle", "tcp_opt", "opt_powerplan"}),
        new("profil_fortnite", "Profil Fortnite", new[]{"FortniteClient-Win64-Shipping"},
            new[]{"clean_temp", "mouse_fix", "kbd_tune", "disable_fso", "disable_dvr", "unlock_fps",
                  "dgpu", "opt_fps", "mmcss", "nagle", "tcp_opt", "opt_powerplan"}),
        new("profil_warzone", "Profil Warzone", new[]{"cod"},
            new[]{"clean_temp", "mouse_fix", "kbd_tune", "disable_fso", "disable_dvr", "unlock_fps",
                  "dgpu", "opt_fps", "power_opt", "mmcss", "nagle", "tcp_opt", "opt_powerplan"}),
        new("profil_fivem", "Profil FiveM", new[]{"FiveM", "FiveM_GTAProcess", "GTA5"},
            new[]{"clean_fivem", "boost1", "disable_fso", "opt_gpu", "mmcss",
                  "nagle", "tcp_opt", "opt_powerplan", "clean_temp"}),
    };

    public static GameProfile? ById(string id) =>
        All.FirstOrDefault(p => p.Id == id);

    public static async Task<(List<string> Succeeded, int Fail, int Prio)> ExecuteAsync(GameProfile profile, IProgress<double>? progress = null)
    {
        var succeeded = new List<string>();
        int fail = 0, i = 0;
        foreach (var actionId in profile.Actions)
        {
            try
            {
                var def = TweakLibrary.ById(actionId);
                if (def == null) { fail++; continue; }
                // La sous-action signe le journal de son propre id (restaure le profil après)
                string? prev = TweakJournal.CurrentAction.Value;
                TweakJournal.CurrentAction.Value = actionId;
                TweakResult res;
                try { res = await def.Run(); }
                finally { TweakJournal.CurrentAction.Value = prev; }
                if (res.Success) succeeded.Add(actionId); else fail++;
            }
            catch (Exception ex)
            {
                fail++;
                Logger.Error($"Profil {profile.Id} action {actionId}", ex);
            }
            i++;
            progress?.Report(i * 100.0 / profile.Actions.Length);
        }
        int prio = ProcessService.SetGamePriority(profile.Processes);
        Logger.Info($"Profil {profile.Title}: {succeeded.Count} OK / {fail} échecs, priorité x{prio}");
        return (succeeded, fail, prio);
    }
}
