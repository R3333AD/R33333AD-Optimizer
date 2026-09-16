using System.IO;

namespace FPSBooster.App.Services;

public static class Logger
{
    private static readonly string LogDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", "logs");

    private static readonly long MaxFileSize = 10 * 1024 * 1024;
    private static readonly int MaxFiles = 5;
    private static string LogFile => Path.Combine(LogDir, $"fpsbooster-{DateTime.Now:yyyyMMdd}.log");

    public static void Info(string msg) => Write("INFO", msg);
    public static void Warn(string msg) => Write("WARN", msg);
    public static void Error(string msg, Exception? ex = null) =>
        Write("ERROR", ex == null ? msg : $"{msg} | {ex.GetType().Name}: {ex.Message}");

    private static void Write(string level, string msg)
    {
        try
        {
            Directory.CreateDirectory(LogDir);
            RotateLogs();
            File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss}] [{level}] {msg}{Environment.NewLine}");
        }
        catch { /* ne jamais crasher l'app pour un log */ }
    }

    private static void RotateLogs()
    {
        try
        {
            if (File.Exists(LogFile) && new FileInfo(LogFile).Length > MaxFileSize)
            {
                string archived = Path.Combine(LogDir, $"fpsbooster-{DateTime.Now:yyyyMMdd-HHmmss}.log");
                File.Move(LogFile, archived);
                var oldFiles = Directory.GetFiles(LogDir, "fpsbooster-*.log")
                    .OrderByDescending(f => File.GetCreationTime(f)).Skip(MaxFiles);
                foreach (var f in oldFiles) File.Delete(f);
            }
        }
        catch (Exception ex)
        {
            // Pas de Logger.Warn ici (récursion si le disque est plein) : échec silencieux volontaire
            System.Diagnostics.Debug.WriteLine($"RotateLogs: {ex.Message}");
        }
    }
}
