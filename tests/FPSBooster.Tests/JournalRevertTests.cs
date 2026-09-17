using NUnit.Framework;
using FPSBooster.App.Models;
using FPSBooster.App.Services;
using Microsoft.Win32;

namespace FPSBooster.Tests;

[TestFixture]
public class JournalRevertTests
{
    [OneTimeSetUp]
    public void Backup() => TestIsolation.BackupSettings();

    [OneTimeTearDown]
    public void Restore() => TestIsolation.RestoreSettings();

    [Test]
    public void Remove_DeletesFirstMatchOnly()
    {
        TweakJournal.AppendReg(RegistryHive.CurrentUser,
            @"Software\FPSBoosterUnitTest", "KeepV", RegistryValueKind.String, false, null);
        TweakJournal.AppendReg(RegistryHive.CurrentUser,
            @"Software\FPSBoosterUnitTest", "DelV", RegistryValueKind.String, false, null);
        TweakJournal.AppendReg(RegistryHive.CurrentUser,
            @"Software\FPSBoosterUnitTest", "KeepV", RegistryValueKind.String, false, null);

        var target = TweakJournal.Load().First(e => e.Name == "DelV");
        Assert.That(TweakJournal.Remove(target), Is.True);

        var names = TweakJournal.Load().Where(e => e.Name is "DelV" or "KeepV").Select(e => e.Name).ToList();
        Assert.That(names, Does.Not.Contain("DelV"));
        Assert.That(names.Count(n => n == "KeepV"), Is.EqualTo(2));
    }

    [Test]
    public void Remove_Missing_ReturnsFalse()
    {
        var ghost = new JournalEntry("2026-01-01T00:00:00", "reg", "CurrentUser",
            @"Software\Nope", "Nope", "String", false, null, "nope");
        Assert.That(TweakJournal.Remove(ghost), Is.False);
    }

    [Test]
    public async Task RevertOne_UnknownArea_ReturnsFalse()
    {
        var e = new JournalEntry("2026-01-01T00:00:00", "xxx", "", "", "", "", false, null, null);
        Assert.That(await RevertService.RevertOneAsync(e), Is.False);
    }

    [Test]
    public void Journal_AppendReg_RecordsActionId()
    {
        TweakJournal.CurrentAction.Value = "test_btn_xyz";
        try
        {
            TweakJournal.AppendReg(RegistryHive.CurrentUser,
                @"Software\FPSBoosterUnitTest", "ActV", RegistryValueKind.DWord, false, null);
            var entry = TweakJournal.Load().LastOrDefault(e => e.Name == "ActV");
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.ActionId, Is.EqualTo("test_btn_xyz"));
        }
        finally { TweakJournal.CurrentAction.Value = null; }
    }
}
