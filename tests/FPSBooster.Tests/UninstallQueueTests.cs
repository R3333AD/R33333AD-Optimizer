using NUnit.Framework;
using FPSBooster.App.Services;
using FPSBooster.App.ViewModels;
using Microsoft.Win32;

namespace FPSBooster.Tests;

[TestFixture]
public class UninstallQueueTests
{
    private static InstalledApp FakeApp(string name, string sub,
        RegistryHive hive = RegistryHive.CurrentUser,
        RegistryView view = RegistryView.Registry64, long sizeKb = -1) =>
        new(name, "1.0", "TestPub", "uninstall.exe", null, sub, hive, view, "—", sizeKb);

    [OneTimeSetUp]
    public void Backup() => TestIsolation.BackupSettings();

    [OneTimeTearDown]
    public void Restore() => TestIsolation.RestoreSettings();

    [Test]
    public void ToggleQueue_AddsThenRemoves()
    {
        var vm = new MainViewModel();
        var app = FakeApp("Demo", "demo");
        vm.ToggleQueue(app);
        Assert.That(vm.QueueCount, Is.EqualTo(1));
        Assert.That(vm.IsQueued(app), Is.True);
        vm.ToggleQueue(app);
        Assert.That(vm.QueueCount, Is.EqualTo(0));
        Assert.That(vm.IsQueued(app), Is.False);
    }

    [Test]
    public void QueueKey_UniquePerHiveViewKey()
    {
        var vm = new MainViewModel();
        vm.ToggleQueue(FakeApp("Demo", "demo", RegistryHive.CurrentUser, RegistryView.Registry64));
        vm.ToggleQueue(FakeApp("Demo", "demo", RegistryHive.LocalMachine, RegistryView.Registry64));
        vm.ToggleQueue(FakeApp("Demo", "demo", RegistryHive.CurrentUser, RegistryView.Registry32));
        Assert.That(vm.QueueCount, Is.EqualTo(3));
    }

    [Test]
    public void PruneQueue_RemovesMissing()
    {
        var vm = new MainViewModel();
        var app = FakeApp("Demo", "demo");
        vm.Apps.Add(app);
        vm.ToggleQueue(app);
        vm.ToggleQueue(FakeApp("Gone", "gone"));
        vm.Apps.Remove(app);
        vm.PruneQueue();
        Assert.That(vm.QueueCount, Is.EqualTo(0));
    }

    [Test]
    public void SizeKb_SortsNumerically()
    {
        var apps = new List<InstalledApp>
        {
            FakeApp("Big", "big", sizeKb: 2_097_152),
            FakeApp("Small", "small", sizeKb: 512),
            FakeApp("Unknown", "unk", sizeKb: -1),
        };
        var ordered = apps.OrderBy(a => a.SizeKb).Select(a => a.DisplayName).ToList();
        Assert.That(ordered, Is.EqualTo(new List<string> { "Unknown", "Small", "Big" }));
    }

    [Test]
    public void GetInstalledApps_ReturnsSortedByName()
    {
        var apps = UninstallerService.GetInstalledApps();
        var names = apps.Select(a => a.DisplayName).ToList();
        Assert.That(names, Is.Ordered);
    }
}
