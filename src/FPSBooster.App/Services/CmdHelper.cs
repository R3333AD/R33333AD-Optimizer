using System.Diagnostics;

namespace FPSBooster.App.Services;

public static class CmdHelper
{
    public static async Task<(int ExitCode, string Out, string Err)> RunAsync(string file, string args, int timeoutMs = 30_000)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = file,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = new Process { StartInfo = psi };
            p.Start();
            var outTask = p.StandardOutput.ReadToEndAsync();
            var errTask = p.StandardError.ReadToEndAsync();
            try
            {
                await p.WaitForExitAsync().WaitAsync(TimeSpan.FromMilliseconds(timeoutMs));
            }
            catch (TimeoutException)
            {
                try { if (!p.HasExited) p.Kill(entireProcessTree: true); }
                catch (Exception killEx) { Logger.Warn($"CMD kill après timeout échoue ({file}): {killEx.Message}"); }
                Logger.Warn($"CMD timeout ({timeoutMs}ms), process tué: {file} {args}");
                return (-1, "", $"Timeout après {timeoutMs}ms");
            }
            string o = await outTask;
            string e = await errTask;
            Logger.Info($"CMD {file} {args} -> exit {p.ExitCode}");
            if (!string.IsNullOrWhiteSpace(e)) Logger.Warn($"CMD err: {e.Trim()}");
            return (p.ExitCode, o, e);
        }
        catch (Exception ex)
        {
            Logger.Error($"CMD FAIL {file} {args}", ex);
            return (-1, "", ex.Message);
        }
    }

    public static Task<(int, string, string)> PowerCfg(string args) => RunAsync("powercfg.exe", args);
    public static Task<(int, string, string)> Netsh(string args) => RunAsync("netsh.exe", args);
    public static Task<(int, string, string)> Ipconfig(string args) => RunAsync("ipconfig.exe", args);
    public static Task<(int, string, string)> Bcdedit(string args) => RunAsync("bcdedit.exe", args);

    public static Task<(int, string, string)> Powershell(string command, int timeoutMs = 30_000) =>
        RunAsync("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"", timeoutMs);
}
