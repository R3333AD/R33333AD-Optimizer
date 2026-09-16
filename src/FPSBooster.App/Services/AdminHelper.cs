using System.Diagnostics;
using System.Security.Principal;

namespace FPSBooster.App.Services;

public static class AdminHelper
{
    public static bool IsAdmin()
    {
        try
        {
            using var id = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(id);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch (Exception ex)
        {
            Logger.Error("IsAdmin check failed", ex);
            return false;
        }
    }

    public static void CreateRestorePoint(string description = "FPSBooster - avant optimisations")
    {
        try
        {
            string safeDesc = description.Replace("'", "''");
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"Checkpoint-Computer -Description '{safeDesc}' -RestorePointType 'MODIFY_SETTINGS'\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            using var p = Process.Start(psi);
            p?.WaitForExit(60_000);
            Logger.Info($"Restore point demandé: {description} exit={p?.ExitCode}");
        }
        catch (Exception ex)
        {
            Logger.Error("CreateRestorePoint failed", ex);
        }
    }
}
