using NUnit.Framework;
using FPSBooster.App.Models;
using FPSBooster.App.Services;
using FPSBooster.App.ViewModels;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Windows.Media;

namespace FPSBooster.Tests;

[TestFixture]
public class UiAndAuditTests
{
    [OneTimeSetUp]
    public void Backup() => TestIsolation.BackupSettings();

    [OneTimeTearDown]
    public void Restore() => TestIsolation.RestoreSettings();

    [Test]
    public void SortResolver_MapsDisplaySizeToSizeKb()
    {
        Assert.That(UiSortHelper.ResolveSortColumn("DisplaySize"), Is.EqualTo("SizeKb"));
        Assert.That(UiSortHelper.ResolveSortColumn("DisplayName"), Is.EqualTo("DisplayName"));
        Assert.That(UiSortHelper.ResolveSortColumn(null), Is.EqualTo("DisplayName"));
        Assert.That(UiSortHelper.ResolveSortColumn(""), Is.EqualTo("DisplayName"));
    }

    [Test]
    public void SortResolver_NextDirection_TogglesAndDefaults()
    {
        Assert.That(UiSortHelper.NextDirection("DisplayName", "DisplayName", ListSortDirection.Ascending),
            Is.EqualTo(ListSortDirection.Descending));
        Assert.That(UiSortHelper.NextDirection("SizeKb", "", ListSortDirection.Ascending),
            Is.EqualTo(ListSortDirection.Descending));
        Assert.That(UiSortHelper.NextDirection("DisplayName", "", ListSortDirection.Ascending),
            Is.EqualTo(ListSortDirection.Ascending));
    }

    [Test]
    public void GainColor_MeasuredHigh_IsGreen()
    {
        var t = new ToolItem { ActionId = "x", GainSort = 5.2, GainText = "mesuré +5.2 %" };
        Assert.That(((SolidColorBrush)t.GainBrush).Color,
            Is.EqualTo(Color.FromRgb(0x00, 0xE6, 0x76)));
    }

    [Test]
    public void GainColor_MeasuredNegative_IsRed()
    {
        var t = new ToolItem { ActionId = "x", GainSort = -2, GainText = "mesuré -2.0 %" };
        Assert.That(((SolidColorBrush)t.GainBrush).Color,
            Is.EqualTo(Color.FromRgb(0xFF, 0x5C, 0x5C)));
    }

    [Test]
    public void GainColor_Estimated_IsLime()
    {
        var t = new ToolItem { ActionId = "x", GainSort = 5.5, GainText = "+3–8 %" };
        Assert.That(((SolidColorBrush)t.GainBrush).Color,
            Is.EqualTo(Color.FromRgb(0xC8, 0xFF, 0x2E)));
    }

    [Test]
    public void GainColor_Dash_IsDarkGray()
    {
        var t = new ToolItem { ActionId = "x", GainSort = -1, GainText = "—" };
        Assert.That(((SolidColorBrush)t.GainBrush).Color,
            Is.EqualTo(Color.FromRgb(0x52, 0x52, 0x5B)));
    }

    [Test]
    public void Journal_RecordsCurrentAction()
    {
        TweakJournal.CurrentAction.Value = "test_action_xyz";
        try
        {
            TweakJournal.AppendReg(RegistryHive.CurrentUser,
                @"Software\FPSBoosterUnitTest", "V", RegistryValueKind.String, false, null);
            var entry = TweakJournal.Load().LastOrDefault(e => e.Name == "V");
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.ActionId, Is.EqualTo("test_action_xyz"));
        }
        finally { TweakJournal.CurrentAction.Value = null; }
    }

    [Test]
    public void Journal_OldFormat_LoadsWithNullAction()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FPSBooster", "journal.jsonl");
        string? saved = File.Exists(path) ? File.ReadAllText(path) : null;
        try
        {
            File.AppendAllText(path,
                "{\"Time\":\"2026-01-01T00:00:00\",\"Area\":\"reg\",\"Hive\":\"CurrentUser\"," +
                "\"Key\":\"Software\\\\X\",\"Name\":\"UnitMig\",\"Kind\":\"String\"," +
                "\"Existed\":false,\"OldValue\":null}\n");
            var entry = TweakJournal.Load().LastOrDefault(e => e.Name == "UnitMig");
            Assert.That(entry, Is.Not.Null);
            Assert.That(entry!.ActionId, Is.Null);
        }
        finally
        {
            if (saved == null) { if (File.Exists(path)) File.Delete(path); }
            else File.WriteAllText(path, saved);
        }
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void MainWindow_ConstructsHeadless()
    {
        if (System.Windows.Application.Current == null)
            _ = new FPSBooster.App.App();
        var w = new FPSBooster.App.MainWindow();
        try
        {
            Assert.That(w.DataContext, Is.InstanceOf<MainViewModel>());
            var vm = (MainViewModel)w.DataContext;
            Assert.That(vm.Sections.Count, Is.EqualTo(12));
            Assert.That(vm.Tools.Count, Is.GreaterThan(0));
        }
        finally { w.Close(); }
    }

    [Test]
    [Apartment(ApartmentState.STA)]
    public void OverlayAndLogWindows_ConstructHeadless()
    {
        if (System.Windows.Application.Current == null)
            _ = new FPSBooster.App.App();
        var o = new FPSBooster.App.Views.OverlayWindow();
        var l = new FPSBooster.App.Views.LogWindow();
        try
        {
            Assert.That(o.IsVisible, Is.False);
            Assert.That(l.IsVisible, Is.False);
        }
        finally
        {
            o.Close();
            l.Close();
        }
    }
}
