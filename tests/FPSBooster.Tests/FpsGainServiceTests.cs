using NUnit.Framework;
using FPSBooster.App.Services;

namespace FPSBooster.Tests;

[TestFixture]
public class FpsGainServiceTests
{
    [Test]
    public void ComputeGainPct_PositiveGain()
    {
        Assert.That(FpsGainService.ComputeGainPct(100, 110), Is.EqualTo(10).Within(0.001));
    }

    [Test]
    public void ComputeGainPct_NegativeGain()
    {
        Assert.That(FpsGainService.ComputeGainPct(100, 95), Is.EqualTo(-5).Within(0.001));
    }

    [Test]
    public void ComputeGainPct_ZeroBefore_ReturnsZero()
    {
        Assert.That(FpsGainService.ComputeGainPct(0, 120), Is.EqualTo(0));
    }

    [Test]
    public void FormatMeasured_Positive_HasPlus()
    {
        string s = FpsGainService.FormatMeasured(4.24);
        Assert.That(s, Does.Contain("+4,2").Or.Contain("+4.2"));
        Assert.That(s, Does.Contain("mesuré"));
    }

    [Test]
    public void FormatMeasured_Negative_NoDoubleSign()
    {
        string s = FpsGainService.FormatMeasured(-2.5);
        Assert.That(s, Does.Not.Contain("+-"));
    }

    [Test]
    public void GainTextOf_Unknown_DefaultsToZero()
    {
        Assert.That(TweakLibrary.GainTextOf("nope_unknown"), Is.EqualTo("~0 %"));
        Assert.That(TweakLibrary.GainSortOf("nope_unknown"), Is.EqualTo(0));
    }

    [Test]
    public void GainTextOf_NonMeasurable_ReturnsDash()
    {
        Assert.That(TweakLibrary.GainTextOf("fps_measure"), Is.EqualTo("—"));
        Assert.That(TweakLibrary.GainSortOf("fps_measure"), Is.LessThan(0));
    }
}
