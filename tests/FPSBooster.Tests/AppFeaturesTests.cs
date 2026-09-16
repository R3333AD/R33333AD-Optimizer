using NUnit.Framework;
using FPSBooster.App.Services;
using FPSBooster.App.ViewModels;
using System.IO;

namespace FPSBooster.Tests;

[TestFixture]
public class AppFeaturesTests
{
    [OneTimeSetUp]
    public void Backup() => TestIsolation.BackupSettings();

    [OneTimeTearDown]
    public void Restore() => TestIsolation.RestoreSettings();

    [Test]
    public void ProfilesSection_ContainsAllGameProfiles()
    {
        var vm = new MainViewModel();
        var section = vm.Sections.FirstOrDefault(s => s.Id == "profils");
        Assert.That(section, Is.Not.Null);
        Assert.That(section!.Number, Is.EqualTo("11"));
        var inSection = TweakLibrary.All
            .Where(d => d.Sections.Contains("profils"))
            .Select(d => d.ActionId).ToList();
        Assert.That(inSection, Is.EquivalentTo(new List<string>
            { "profil_valorant", "profil_fortnite", "profil_warzone", "profil_fivem" }));
        // Les profils ne sont plus dans la section FPS
        Assert.That(TweakLibrary.All.Where(d => d.ActionId.StartsWith("profil_")).All(d => d.Sections.SequenceEqual(new[] { "profils" })), Is.True);
    }

    [Test]
    public void MainViewModel_SelectSection_UpdatesSelection()
    {
        var vm = new MainViewModel();
        var clean = vm.Sections.First(s => s.Id == "clean");
        vm.SelectSection(clean);
        Assert.That(vm.SelectedSection.Id, Is.EqualTo("clean"));
        Assert.That(clean.IsSelected, Is.True);
        Assert.That(vm.Sections.Count(s => s.IsSelected), Is.EqualTo(1));
    }

    [Test]
    public void MainViewModel_SetRisk_Clamps()
    {
        var vm = new MainViewModel();
        vm.SetRisk(5);
        Assert.That(vm.MaxRisk, Is.EqualTo(2));
        vm.SetRisk(-1);
        Assert.That(vm.MaxRisk, Is.EqualTo(0));
        vm.SetRisk(1);
        Assert.That(SettingsService.Get("risk"), Is.EqualTo("1"));
    }

    [Test]
    public void MainViewModel_ToolsHaveGains()
    {
        var vm = new MainViewModel();
        Assert.That(vm.Tools.Count, Is.GreaterThan(0));
        Assert.That(vm.Tools.All(t => !string.IsNullOrEmpty(t.GainText)), Is.True);
        Assert.That(vm.FindTool("boost1"), Is.Not.Null);
        Assert.That(vm.FindTool("nope"), Is.Null);
    }

    [Test]
    public async Task PrereqService_ReturnsFiveChecks()
    {
        var list = await PrereqService.CheckAsync();
        Assert.That(list.Count, Is.EqualTo(5));
        Assert.That(list.All(p => !string.IsNullOrEmpty(p.Name)), Is.True);
    }

    [Test]
    public async Task StartupService_IsAutoStart_DoesNotThrow()
    {
        Assert.DoesNotThrowAsync(async () => await StartupService.IsAutoStartAsync());
    }

    [Test]
    public void ConfigBackup_Export_CreatesZip()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "fpsbooster-test-" + Guid.NewGuid().ToString("N"));
        try
        {
            var res = ConfigBackupService.Export(tmp);
            Assert.That(res.Success, Is.True, res.Message);
            Assert.That(File.Exists(res.Message), Is.True);
        }
        finally
        {
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true); } catch { }
        }
    }

    [Test]
    public void ConfigBackup_Import_InvalidZip_Fails()
    {
        string tmp = Path.Combine(Path.GetTempPath(), "fpsbooster-test-" + Guid.NewGuid().ToString("N") + ".zip");
        try
        {
            File.WriteAllText(tmp, "pas un zip");
            var res = ConfigBackupService.ImportFile(tmp);
            Assert.That(res.Success, Is.False);
        }
        finally
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
        }
    }

    [Test]
    public async Task DiskHealth_Query_DoesNotThrow()
    {
        var list = await DiskHealthService.QueryAsync();
        Assert.That(list, Is.Not.Null);
    }

    [Test]
    public void DiskHealth_Icon_MapsKnownStates()
    {
        Assert.That(DiskHealthService.IconOf("Healthy"), Is.EqualTo("✓"));
        Assert.That(DiskHealthService.IconOf("Unhealthy"), Is.EqualTo("✕"));
        Assert.That(DiskHealthService.IconOf("???"), Is.EqualTo("?"));
    }

    [Test]
    public void IncludedTools_AllIdsExist()
    {
        var allIds = new HashSet<string>(TweakLibrary.All.Select(d => d.ActionId), StringComparer.OrdinalIgnoreCase);
        foreach (var pack in new[] { "opt_entire", "boost3", "boost_fivem" })
            Assert.That(TweakLibrary.IncludedOf(pack).All(id => allIds.Contains(id)), Is.True, pack);
        Assert.That(TweakLibrary.IncludedOf("nope_unknown"), Is.Empty);
    }

    [Test]
    public void IncludedTools_Boost3_ContainsCoreSkipsPartial()
    {
        var subs = TweakLibrary.IncludedOf("boost3").ToList();
        Assert.That(subs, Does.Contain("disable_dvr"));
        Assert.That(subs, Does.Contain("tcp_opt"));
        Assert.That(subs, Does.Contain("svc_sysmain"));
        Assert.That(subs, Does.Not.Contain("power_opt")); // recouvrement partiel seulement
    }

    [Test]
    public void BoostAllMsg_FormatsFiveArgs()
    {
        foreach (var lang in new[] { "fr", "ar", "en" })
        {
            Loc.Instance.Set(lang);
            string msg = Loc.Instance.Get("Dlg_BoostAllMsg", "FPS", 3, "CMD : 2 • REG : 1", "Normal ×3", "A\n• B");
            Assert.That(msg, Does.Contain("3"));
            Assert.That(msg, Does.Not.Contain("{0}"));
        }
        Loc.Instance.Set("fr");
    }

    [Test]
    public void UpdateService_DefaultUrl_PointsToGitHubReleases()
    {
        Assert.That(UpdateService.DefaultUpdateUrl,
            Is.EqualTo("https://github.com/R3333AD/R33333AD-Optimizer/releases/latest/download/version.txt"));
    }

    [Test]
    public async Task UpdateService_EmptyUrl_DisabledWithoutNetwork()
    {
        SettingsService.Set("update_url", "");
        var (found, msg) = await UpdateService.CheckAsync();
        Assert.That(found, Is.False);
        Assert.That(msg, Does.Contain("aucune URL").Or.Contain("URL"));
    }
}
