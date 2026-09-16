namespace FPSBooster.App.Services;

/// <summary>Santé des disques via Get-PhysicalDisk (sans package). Cache 5 min.</summary>
public static class DiskHealthService
{
    public record DiskInfo(string Name, string Health);

    private static readonly object Lock = new();
    private static DateTime _nextCheck = DateTime.MinValue;
    private static List<DiskInfo> _cached = new();

    public static string IconOf(string health) => health switch
    {
        "Healthy" => "✓",
        "Unhealthy" or "Warning" => "✕",
        _ => "?",
    };

    public static List<DiskInfo> GetCached()
    {
        lock (Lock)
        {
            if (DateTime.Now < _nextCheck) return _cached.ToList();
            _nextCheck = DateTime.Now.AddMinutes(5);
        }
        _ = Task.Run(async () =>
        {
            try
            {
                var fresh = await QueryAsync();
                lock (Lock) { _cached = fresh; }
            }
            catch (Exception ex) { Logger.Warn($"Disques fond: {ex.Message}"); }
        });
        lock (Lock) { return _cached.ToList(); }
    }

    public static async Task<List<DiskInfo>> QueryAsync()
    {
        var list = new List<DiskInfo>();
        try
        {
            var (code, stdout, _) = await CmdHelper.Powershell(
                "Get-PhysicalDisk | ForEach-Object { '{0}|{1}' -f $_.FriendlyName, $_.HealthStatus }", 15_000);
            if (code != 0) return list;
            foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var parts = line.Split('|');
                if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[0])) continue;
                list.Add(new DiskInfo(parts[0].Trim(), parts[1].Trim()));
            }
            Logger.Info($"Disques: {list.Count} détecté(s)");
        }
        catch (Exception ex) { Logger.Warn($"Disques: {ex.Message}"); }
        return list;
    }
}
