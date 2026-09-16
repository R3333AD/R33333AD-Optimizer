using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FPSBooster.App.Services;

/// <summary>Monitoring CPU/RAM sans package NuGet (P/Invoke pur) + GPU via nvidia-smi.</summary>
public sealed class MonitoringService : IDisposable
{
    [StructLayout(LayoutKind.Sequential)]
    private struct FILETIME { public uint dwLowDateTime; public uint dwHighDateTime; }
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetSystemTimes(out FILETIME idle, out FILETIME kernel, out FILETIME user);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad; public ulong ullTotalPhys; public ulong ullAvailPhys;
        public ulong ullTotalPageFile; public ulong ullAvailPageFile; public ulong ullTotalVirtual;
        public ulong ullAvailVirtual; public ulong ullAvailExtendedVirtual;
    }
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    private static ulong ToUlong(FILETIME ft) => ((ulong)ft.dwHighDateTime << 32) | ft.dwLowDateTime;
    private FILETIME _prevIdle, _prevKernel, _prevUser;
    private bool _first = true;

    public double GetCpuPercent()
    {
        try
        {
            if (!GetSystemTimes(out var idle, out var kernel, out var user)) return 0;
            if (_first)
            {
                _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
                _first = false;
                return 0;
            }
            ulong idleDiff = ToUlong(idle) - ToUlong(_prevIdle);
            ulong kernDiff = ToUlong(kernel) - ToUlong(_prevKernel);
            ulong userDiff = ToUlong(user) - ToUlong(_prevUser);
            _prevIdle = idle; _prevKernel = kernel; _prevUser = user;
            ulong total = kernDiff + userDiff;
            if (total == 0) return 0;
            double cpu = (double)(total - idleDiff) * 100.0 / total;
            return Math.Clamp(cpu, 0, 100);
        }
        catch { return 0; }
    }

    public double GetRamPercent()
    {
        try
        {
            var mem = new MEMORYSTATUSEX();
            if (!GlobalMemoryStatusEx(mem)) return 0;
            return mem.dwMemoryLoad;
        }
        catch { return 0; }
    }

    private static readonly object GpuLock = new();
    private static DateTime _gpuNextCheck = DateTime.MinValue;
    private static string _gpuCached = "—";

    /// <summary>Texte GPU "54° • 12%" (nvidia-smi), "—" si indisponible (AMD/Intel).
    /// Non bloquant : la requête tourne en tâche de fond, au max toutes les 10 s.</summary>
    public string GetGpuText()
    {
        try
        {
            lock (GpuLock)
            {
                if (DateTime.Now < _gpuNextCheck) return _gpuCached;
                _gpuNextCheck = DateTime.Now.AddSeconds(10);
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    string? txt = await QueryNvidiaSmiAsync();
                    lock (GpuLock) { _gpuCached = txt ?? "—"; }
                }
                catch (Exception ex) { Logger.Warn($"GPU fond: {ex.Message}"); }
            });
            lock (GpuLock) { return _gpuCached; }
        }
        catch (Exception ex)
        {
            Logger.Warn($"GPU: {ex.Message}");
            return "—";
        }
    }

    private static async Task<string?> QueryNvidiaSmiAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "nvidia-smi",
                Arguments = "--query-gpu=temperature.gpu,utilization.gpu --format=csv,noheader,nounits",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = new Process { StartInfo = psi };
            p.Start();
            var outTask = p.StandardOutput.ReadToEndAsync();
            try
            {
                await p.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(8));
            }
            catch (TimeoutException)
            {
                try { if (!p.HasExited) p.Kill(entireProcessTree: true); } catch { }
                return null;
            }
            if (p.ExitCode != 0) return null;
            string line = (await outTask).Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .FirstOrDefault()?.Trim() ?? "";
            var parts = line.Split(',');
            if (parts.Length < 2) return null;
            if (!int.TryParse(parts[0].Trim(), out int temp)) return null;
            if (!int.TryParse(parts[1].Trim().TrimEnd('%', ' '), out int util)) return null;
            return $"{temp}° • {util}%";
        }
        catch
        {
            return null; // nvidia-smi absent (AMD/Intel) : silencieux
        }
    }

    public void Dispose() { }
}
