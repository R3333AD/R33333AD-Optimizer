using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace FPSBooster.App.Services;

public record BenchResult(double CpuMs, double RamMs, double DiskMs, int Score, string Summary);

/// <summary>Benchmark CPU/RAM/Disque + score /100 + comparaison à la référence.</summary>
public static class BenchmarkService
{
    private static readonly string BasePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", "bench.json");

    // Références (machine milieu de gamme typique) pour un score absolu indicatif
    private const double CpuRefMs = 1500, RamRefMs = 400, DiskRefMs = 900;

    public static async Task<BenchResult> RunAsync()
    {
        double cpu = await Task.Run(MeasureCpu);
        double ram = await Task.Run(MeasureRam);
        double disk = await Task.Run(MeasureDisk);
        int score = (int)Math.Round((Component(cpu, CpuRefMs) + Component(ram, RamRefMs) + Component(disk, DiskRefMs)) / 3.0);

        string summary;
        var prev = LoadBaseline();
        if (prev == null)
        {
            SaveBaseline(cpu, ram, disk, score);
            summary = $"Score {score}/100 — référence enregistrée (relance après tes optimisations pour comparer)";
        }
        else
        {
            double dCpu = Pct(prev.CpuMs, cpu), dRam = Pct(prev.RamMs, ram), dDisk = Pct(prev.DiskMs, disk);
            double dScore = score - prev.Score;
            // La référence d'origine est conservée (pas d'écrasement) pour comparer dans le temps
            summary = $"Score {score}/100 ({Sign(dScore)}pts) | CPU {Sign(dCpu)}% RAM {Sign(dRam)}% Disque {Sign(dDisk)}%";
        }
        Logger.Info($"BENCH CPU {cpu:0}ms RAM {ram:0}ms DISK {disk:0}ms SCORE {score} | {summary}");
        SaveRun(cpu, ram, disk, score);
        return new BenchResult(cpu, ram, disk, score, summary);
    }

    private static double Component(double actual, double reference) =>
        Math.Clamp(100.0 * reference / Math.Max(actual, 1), 5, 100);

    private static double Pct(double before, double after) =>
        before <= 0 ? 0 : (before - after) / before * 100.0; // + = plus rapide

    private static string Sign(double v) => v >= 0 ? $"+{v:0}" : $"{v:0}";

    private static double MeasureCpu()
    {
        // Crible d'Ératosthène jusqu'à 500k + hash (charge CPU pure, déterministe)
        var sw = Stopwatch.StartNew();
        const int n = 500_000;
        var sieve = new bool[n + 1];
        Array.Fill(sieve, true);
        for (int i = 2; i * i <= n; i++)
            if (sieve[i])
                for (int j = i * i; j <= n; j += i) sieve[j] = false;
        int primes = sieve.Count(b => b);
        byte[] buf = new byte[1_048_576];
        new Random(42).NextBytes(buf);
        for (int i = 0; i < 20; i++) buf = SHA256.HashData(buf);
        sw.Stop();
        Logger.Info($"BENCH CPU: {primes} premiers, {sw.ElapsedMilliseconds}ms");
        return sw.Elapsed.TotalMilliseconds;
    }

    private static double MeasureRam()
    {
        var sw = Stopwatch.StartNew();
        byte[] buf = new byte[64 * 1024 * 1024];
        for (int i = 0; i < buf.Length; i += 4096) buf[i] = (byte)(i & 0xFF);
        long sum = 0;
        for (int i = 0; i < buf.Length; i += 64) sum += buf[i];
        sw.Stop();
        GC.KeepAlive(sum);
        Logger.Info($"BENCH RAM: 64Mo, {sw.ElapsedMilliseconds}ms");
        return sw.Elapsed.TotalMilliseconds;
    }

    private static double MeasureDisk()
    {
        string path = Path.Combine(Path.GetTempPath(), "fpsbooster-bench.tmp");
        byte[] buf = new byte[32 * 1024 * 1024];
        new Random(7).NextBytes(buf);
        var sw = Stopwatch.StartNew();
        File.WriteAllBytes(path, buf);
        byte[] back = File.ReadAllBytes(path);
        sw.Stop();
        try { File.Delete(path); }
        catch (Exception ex) { Logger.Warn($"BENCH DISK nettoyage tmp: {ex.Message}"); }
        GC.KeepAlive(back.Length);
        Logger.Info($"BENCH DISK: 32Mo R+W, {sw.ElapsedMilliseconds}ms");
        return sw.Elapsed.TotalMilliseconds;
    }

    private record Baseline(double CpuMs, double RamMs, double DiskMs, int Score, string Date);

    public record BenchRun(double CpuMs, double RamMs, double DiskMs, int Score, string Date);

    private static readonly string HistoryPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", "bench-history.json");

    private const int MaxHistory = 20;

    public static List<BenchRun> LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryPath)) return new();
            return JsonSerializer.Deserialize<List<BenchRun>>(File.ReadAllText(HistoryPath)) ?? new();
        }
        catch (Exception ex)
        {
            Logger.Warn($"Bench history load: {ex.Message}");
            return new();
        }
    }

    public static void SaveRun(double cpu, double ram, double disk, int score)
    {
        try
        {
            var h = LoadHistory();
            h.Add(new BenchRun(cpu, ram, disk, score, DateTime.Now.ToString("o")));
            while (h.Count > MaxHistory) h.RemoveAt(0);
            Directory.CreateDirectory(Path.GetDirectoryName(HistoryPath)!);
            File.WriteAllText(HistoryPath, JsonSerializer.Serialize(h));
        }
        catch (Exception ex) { Logger.Error("Bench history save", ex); }
    }

    public static string BaselineSummary()
    {
        var b = LoadBaseline();
        return b == null ? "aucune référence" : $"référence {b.Score}/100 du {b.Date[..10]}";
    }

    private static Baseline? LoadBaseline()
    {
        try
        {
            if (!File.Exists(BasePath)) return null;
            return JsonSerializer.Deserialize<Baseline>(File.ReadAllText(BasePath));
        }
        catch { return null; }
    }

    private static void SaveBaseline(double cpu, double ram, double disk, int score)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BasePath)!);
            File.WriteAllText(BasePath, JsonSerializer.Serialize(new Baseline(cpu, ram, disk, score, DateTime.Now.ToString("o"))));
        }
        catch (Exception ex) { Logger.Error("Bench save", ex); }
    }
}
