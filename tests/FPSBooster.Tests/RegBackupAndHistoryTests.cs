using NUnit.Framework;
using FPSBooster.App.Services;
using Microsoft.Win32;
using System.IO;

namespace FPSBooster.Tests;

[TestFixture]
public class RegBackupAndHistoryTests
{
    private static string RegBackupPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", $"regbackup-{DateTime.Now:yyyyMMdd}.reg");

    private static string GainsPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "FPSBooster", "fpsgains.json");

    [OneTimeSetUp]
    public void Backup() => TestIsolation.BackupSettings();

    [OneTimeTearDown]
    public void Restore() => TestIsolation.RestoreSettings();

    [Test]
    public void RegBackup_DWord_WritesHex8()
    {
        RegBackupService.AppendSet(RegistryHive.CurrentUser, @"Software\FPSBoosterUnitTest",
            "DwordV", 0x12345678, RegistryValueKind.DWord, true);
        string content = File.ReadAllText(RegBackupPath());
        Assert.That(content, Does.Contain("Windows Registry Editor Version 5.00"));
        Assert.That(content, Does.Contain("\"DwordV\"=dword:12345678"));
    }

    [Test]
    public void RegBackup_DeletedValue_WritesMinus()
    {
        RegBackupService.AppendSet(RegistryHive.CurrentUser, @"Software\FPSBoosterUnitTest",
            "DelV", null, RegistryValueKind.String, false);
        Assert.That(File.ReadAllText(RegBackupPath()), Does.Contain("\"DelV\"=-"));
    }

    [Test]
    public void RegBackup_String_EscapesQuotes()
    {
        RegBackupService.AppendSet(RegistryHive.CurrentUser, @"Software\FPSBoosterUnitTest",
            "StrV", "a\"b\\c", RegistryValueKind.String, true);
        Assert.That(File.ReadAllText(RegBackupPath()), Does.Contain("\"StrV\"=\"a\\\"b\\\\c\""));
    }

    [Test]
    public void RegBackup_Binary_WritesHex()
    {
        RegBackupService.AppendSet(RegistryHive.CurrentUser, @"Software\FPSBoosterUnitTest",
            "BinV", new byte[] { 0xDE, 0xAD }, RegistryValueKind.Binary, true);
        Assert.That(File.ReadAllText(RegBackupPath()), Does.Contain("\"BinV\"=hex:de,ad"));
    }

    [Test]
    public void FpsGain_History_TrimmedToFive()
    {
        const string id = "test_hist_trim";
        for (int i = 1; i <= 7; i++)
            FpsGainService.SaveGain(id, 100, 100 + i, i);
        var list = FpsGainService.LoadHistory()[id];
        Assert.That(list.Count, Is.EqualTo(5));
        Assert.That(list[^1].GainPct, Is.EqualTo(7).Within(0.001));
        Assert.That(list[0].GainPct, Is.EqualTo(3).Within(0.001));
    }

    [Test]
    public void FpsGain_Average_OfKeptFive()
    {
        const string id = "test_hist_avg";
        foreach (var g in new[] { 10.0, 20.0, 30.0 })
            FpsGainService.SaveGain(id, 100, 100 + g, g);
        Assert.That(FpsGainService.Load()[id].GainPct, Is.EqualTo(20).Within(0.001));
    }

    [Test]
    public void FpsGain_Migration_OldFormat()
    {
        string path = GainsPath();
        string? saved = File.Exists(path) ? File.ReadAllText(path) : null;
        try
        {
            File.WriteAllText(path,
                "{\"test_hist_mig\":{\"Before\":100,\"After\":110,\"GainPct\":10,\"Date\":\"2026-01-01T00:00:00\"}}");
            var h = FpsGainService.LoadHistory();
            Assert.That(h["test_hist_mig"].Count, Is.EqualTo(1));
            Assert.That(h["test_hist_mig"][0].GainPct, Is.EqualTo(10).Within(0.001));
        }
        finally
        {
            if (saved == null) { if (File.Exists(path)) File.Delete(path); }
            else File.WriteAllText(path, saved);
        }
    }

    [Test]
    public void Bench_History_AppendsRuns()
    {
        BenchmarkService.SaveRun(1, 2, 3, 41);
        BenchmarkService.SaveRun(1, 2, 3, 42);
        BenchmarkService.SaveRun(1, 2, 3, 43);
        var last3 = BenchmarkService.LoadHistory().TakeLast(3).Select(r => r.Score).ToList();
        Assert.That(last3, Is.EqualTo(new List<int> { 41, 42, 43 }));
    }

    [Test]
    public void Bench_BaselineSummary_ReturnsText()
    {
        Assert.That(BenchmarkService.BaselineSummary(), Is.Not.Null.And.Not.Empty);
    }
}
