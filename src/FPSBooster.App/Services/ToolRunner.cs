using FPSBooster.App.Models;

namespace FPSBooster.App.Services;

public static class ToolRunner
{
    public static async Task<TweakResult> RunAsync(ToolItem item)
    {
        var def = TweakLibrary.ById(item.ActionId);
        if (def == null) return new(false, "Action inconnue : " + item.ActionId);
        if (!AdminHelper.IsAdmin())
            Logger.Warn($"Exécution sans admin : {item.ActionId} (certains tweaks HKLM/powercfg peuvent échouer)");

        item.IsRunning = true;
        string? prevAction = TweakJournal.CurrentAction.Value;
        TweakJournal.CurrentAction.Value = item.ActionId;
        try
        {
            var res = await def.Run();
            if (res.Success) item.IsApplied = true;
            Logger.Info($"{item.ActionId} -> {(res.Success ? "OK" : "FAIL")} : {res.Message}");
            return res;
        }
        catch (Exception ex)
        {
            Logger.Error($"Runner {item.ActionId}", ex);
            return new(false, ex.Message);
        }
        finally
        {
            TweakJournal.CurrentAction.Value = prevAction;
            item.IsRunning = false;
        }
    }

    public static async Task<(int ok, int fail)> RunAllAsync(IEnumerable<ToolItem> items, IProgress<double>? progress = null)
    {
        int ok = 0, fail = 0, i = 0;
        var list = items.ToList();
        if (list.Count == 0) return (0, 0);
        foreach (var item in list)
        {
            var r = await RunAsync(item);
            if (r.Success) ok++; else fail++;
            i++;
            progress?.Report(i * 100.0 / list.Count);
        }
        return (ok, fail);
    }
}
