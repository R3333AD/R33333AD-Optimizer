using NUnit.Framework;
using FPSBooster.App.Services;
using System.IO;

namespace FPSBooster.Tests;

[TestFixture]
public class SettingsServiceTests
{
    private const string TestKey = "test_unit_key";
    private const string TestValue = "test_value_123";

    [OneTimeSetUp]
    public void BackupUserSettings() => TestIsolation.BackupSettings();

    [OneTimeTearDown]
    public void RestoreUserSettings() => TestIsolation.RestoreSettings();

    [SetUp]
    public void Setup()
    {
        SettingsService.Set(TestKey, TestValue);
    }

    [Test]
    public void Get_ExistingKey_ReturnsValue()
    {
        string result = SettingsService.Get(TestKey);
        Assert.That(result, Is.EqualTo(TestValue));
    }

    [Test]
    public void Get_NonExistingKey_ReturnsDefault()
    {
        string result = SettingsService.Get("nonexistent_key", "default_val");
        Assert.That(result, Is.EqualTo("default_val"));
    }

    [Test]
    public void Set_UpdatesCacheAndFile()
    {
        const string newKey = "test_set_key";
        const string newVal = "new_value";
        SettingsService.Set(newKey, newVal);
        string result = SettingsService.Get(newKey);
        Assert.That(result, Is.EqualTo(newVal));
    }

    [Test]
    public void GetDarkMode_WhenSetFalse_ReturnsFalse()
    {
        SettingsService.Set("darkmode", "false");
        bool result = SettingsService.GetDarkMode();
        Assert.That(result, Is.False);
    }

    [Test]
    public void SetDarkMode_True_ReturnsTrue()
    {
        SettingsService.Set("darkmode", "true");
        bool result = SettingsService.GetDarkMode();
        Assert.That(result, Is.True);
    }

    [Test]
    public void Get_Lang_ReturnsSavedLanguage()
    {
        SettingsService.Set("lang", "fr");
        string result = SettingsService.Get("lang");
        Assert.That(result, Is.EqualTo("fr"));
    }
}
