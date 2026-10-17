using VolumeX.Audio;

namespace VolumeX.Tests;

public sealed class ToneCurveTests
{
    [TestCase(20, 1)]
    [TestCase(50, 1)]
    [TestCase(400, 0)]
    [TestCase(10000, 0)]
    public void BassWeight_IsFullBelowAndGoneAboveTheShelf(double frequency, double expected)
    {
        Assert.That(ToneCurve.BassWeight(frequency), Is.EqualTo(expected));
    }

    [Test]
    public void BassWeight_FallsSmoothlyThroughTheMiddle()
    {
        var weights = Enumerable.Range(0, 60).Select(step => ToneCurve.BassWeight(50 * Math.Pow(8, step / 59.0))).ToArray();

        Assert.That(weights, Is.Ordered.Descending);
        Assert.That(ToneCurve.BassWeight(Math.Sqrt(50 * 400)), Is.EqualTo(0.5).Within(1e-9));
    }

    [Test]
    public void NothingOn_IsFlat()
    {
        var tone = ToneState.Off with { EqualizerGainsDecibels = EqualizerPresets.Find("rock").GainsDecibels, BassDecibels = 9 };

        Assert.That(ToneCurve.Levels([31.25, 1000, 16000], tone), Is.EqualTo(new double[] { 0, 0, 0 }));
    }

    [Test]
    public void EqualizerAndBass_AddUp()
    {
        var tone = ToneState.Off with
        {
            IsEqualizerEnabled = true,
            EqualizerGainsDecibels = EqualizerPresets.Find("rock").GainsDecibels,
            IsBassEnabled = true,
            BassDecibels = -6,
        };

        var levels = ToneCurve.Levels([31.25, 1000, 16000], tone);

        Assert.That(levels, Is.EqualTo(new double[] { 5 - 6, -1, 5 }));
    }

    [TestCase(100, 9)]
    [TestCase(-100, -9)]
    [TestCase(5.4, 5.5)]
    [TestCase(5.2, 5)]
    [TestCase(-5.25, -5.5)]
    [TestCase(double.NaN, ToneCurve.DefaultBassDecibels)]
    public void Bass_KeepsTheDialBoundaries(double requested, double expected)
    {
        Assert.That(ToneCurve.NormalizeBass(requested), Is.EqualTo(expected));
    }
}
