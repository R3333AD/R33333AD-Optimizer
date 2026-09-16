using NUnit.Framework;
using FPSBooster.App.Services;

namespace FPSBooster.Tests;

[TestFixture]
public class LocTests
{
    private string _savedLang = "fr";

    [OneTimeSetUp]
    public void BackupUserSettings() => TestIsolation.BackupSettings();

    [OneTimeTearDown]
    public void RestoreUserSettings() => TestIsolation.RestoreSettings();

    [SetUp]
    public void SaveLang() => _savedLang = Loc.Instance.Current;

    [TearDown]
    public void RestoreLang()
    {
        if (Loc.Instance.Current != _savedLang)
            Loc.Instance.Set(_savedLang);
    }
    [Test]
    public void Get_ExistingFrenchKey_ReturnsValue()
    {
        string result = Loc.Instance.Get("Nav_Home");
        Assert.That(result, Is.EqualTo("Home"));
    }

    [Test]
    public void Get_ExistingArabicKey_ReturnsArabicValue()
    {
        Loc.Instance.Set("ar");
        string result = Loc.Instance.Get("Nav_Home");
        Assert.That(result, Is.EqualTo("الرئيسية"));
    }

    [Test]
    public void Get_ExistingEnglishKey_ReturnsEnglishValue()
    {
        Loc.Instance.Set("en");
        string result = Loc.Instance.Get("Nav_Home");
        Assert.That(result, Is.EqualTo("Home"));
    }

    [Test]
    public void Get_MissingKey_ReturnsKey()
    {
        string result = Loc.Instance.Get("nonexistent_key_xyz");
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Does.Contain("nonexistent_key_xyz"));
    }

    [Test]
    public void Current_ReturnsCurrentLanguage()
    {
        string current = Loc.Instance.Current;
        Assert.That(current, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public void IsRtl_ReturnsTrueForArabic()
    {
        Loc.Instance.Set("ar");
        Assert.That(Loc.Instance.IsRtl, Is.True);
    }

    [Test]
    public void IsRtl_ReturnsFalseForFrench()
    {
        Assert.That(Loc.Instance.IsRtl, Is.False);
    }

    [Test]
    public void Set_InvalidLanguage_DoesNotChange()
    {
        string before = Loc.Instance.Current;
        Loc.Instance.Set("xx");
        Assert.That(Loc.Instance.Current, Is.EqualTo(before));
    }

    [Test]
    public void Get_AllLanguagesHaveNavKeys()
    {
        foreach (var lang in new[] { "fr", "ar", "en" })
        {
            Loc.Instance.Set(lang);
            string result = Loc.Instance.Get("Nav_Home");
            Assert.That(result, Is.Not.Null.And.Not.Empty, $"Langue {lang} devrait avoir Nav_Home");
        }
    }
}
