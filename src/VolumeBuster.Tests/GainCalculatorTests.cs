using VolumeBuster.Audio;

namespace VolumeBuster.Tests;

public sealed class GainCalculatorTests
{
    [TestCase(100, 0.0)]
    [TestCase(105, 1.2714)]
    [TestCase(110, 2.4836)]
    [TestCase(125, 5.8146)]
    [TestCase(150, 10.5655)]
    [TestCase(200, 18.0618)]
    public void PercentToDecibels_UsesTheCubicLinuxScale(double percent, double expectedDecibels)
    {
        Assert.That(GainCalculator.PercentToDecibels(percent), Is.EqualTo(expectedDecibels).Within(0.0001));
    }

    [TestCase(100, 1.0)]
    [TestCase(125, 1.953125)]
    [TestCase(150, 3.375)]
    public void PercentToAmplitude_IsTheCubeOfTheRatio(double percent, double expectedAmplitude)
    {
        Assert.That(GainCalculator.PercentToAmplitude(percent), Is.EqualTo(expectedAmplitude).Within(1e-12));
    }

    [Test]
    public void PercentToDecibels_MatchesTheAmplitude()
    {
        var decibels = GainCalculator.PercentToDecibels(135);

        Assert.That(decibels, Is.EqualTo(20 * Math.Log10(GainCalculator.PercentToAmplitude(135))).Within(1e-12));
    }

    [TestCase(100, 0)]
    [TestCase(105, 127)]
    [TestCase(125, 581)]
    [TestCase(150, 1057)]
    public void PercentToMillibels_RoundsToWholeMillibels(double percent, int expectedMillibels)
    {
        Assert.That(GainCalculator.PercentToMillibels(percent), Is.EqualTo(expectedMillibels));
    }

    [TestCase(0.0, 0)]
    [TestCase(6.0206, 602)]
    [TestCase(0.005, 1)]
    [TestCase(-0.005, -1)]
    [TestCase(-3.5, -350)]
    public void DecibelsToMillibels_RoundsHalfAwayFromZero(double decibels, int expectedMillibels)
    {
        Assert.That(GainCalculator.DecibelsToMillibels(decibels), Is.EqualTo(expectedMillibels));
    }

    [TestCase(50, 100)]
    [TestCase(99.9, 100)]
    [TestCase(100, 100)]
    [TestCase(102.4, 100)]
    [TestCase(102.5, 105)]
    [TestCase(112, 110)]
    [TestCase(113, 115)]
    [TestCase(150, 150)]
    [TestCase(150.1, 150)]
    [TestCase(1000, 150)]
    public void Normalize_ClampsToTheSliderAndSnapsToSteps(double percent, double expected)
    {
        Assert.That(GainCalculator.Normalize(percent), Is.EqualTo(expected));
    }

    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    [TestCase(double.NegativeInfinity)]
    public void Normalize_TurnsGarbageIntoNoBoost(double percent)
    {
        Assert.That(GainCalculator.Normalize(percent), Is.EqualTo(GainCalculator.MinPercent));
    }

    [Test]
    public void SliderRange_StartsAtTheSystemMaximumAndDividesIntoWholeSteps()
    {
        Assert.That(GainCalculator.MinPercent, Is.EqualTo(100));
        Assert.That(GainCalculator.PercentToDecibels(GainCalculator.MinPercent), Is.Zero);
        Assert.That(GainCalculator.MaxPercent, Is.GreaterThan(GainCalculator.MinPercent));
        Assert.That((GainCalculator.MaxPercent - GainCalculator.MinPercent) % GainCalculator.StepPercent, Is.Zero);
        Assert.That(GainCalculator.DefaultPercent, Is.InRange(GainCalculator.MinPercent, GainCalculator.MaxPercent));
    }
}
