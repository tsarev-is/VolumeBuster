using VolumeBuster.Audio;

namespace VolumeBuster.Tests;

public sealed class EqualizerCurveTests
{
    [Test]
    public void Layouts_AreEquallySpacedUpTo16Kilohertz()
    {
        Assert.That(EqualizerLayout.Five.Frequencies, Is.EqualTo(new[] { 62.5, 250, 1000, 4000, 16000 }).Within(0.01));
        Assert.That(EqualizerLayout.Ten.Frequencies, Is.EqualTo(new[] { 31.25, 62.5, 125, 250, 500, 1000, 2000, 4000, 8000, 16000 }).Within(0.01));
        Assert.That(EqualizerLayout.Seven.Frequencies, Is.EqualTo(new[] { 62.5, 157.5, 396.9, 1000, 2519.8, 6349.6, 16000 }).Within(0.1));
        Assert.That(EqualizerLayout.All, Has.All.Matches<EqualizerLayout>(layout => layout.Labels.Count == layout.BandCount));
    }

    [TestCase(5, 5)]
    [TestCase(7, 7)]
    [TestCase(10, 10)]
    [TestCase(0, 10)]
    [TestCase(31, 10)]
    public void For_FallsBackToTheDefaultLayout(int bandCount, int expected)
    {
        Assert.That(EqualizerLayout.For(bandCount).BandCount, Is.EqualTo(expected));
    }

    [Test]
    public void Curve_PassesThroughEverySlider()
    {
        double[] gains = [3, -2, 7.5, 0, -12, 4, 4, 1, -1, 12];

        var heard = EqualizerLayout.Ten.Frequencies.Select(frequency => EqualizerCurve.DecibelsAt(EqualizerLayout.Ten, gains, frequency));

        Assert.That(heard, Is.EqualTo(gains).Within(1e-9));
    }

    [Test]
    public void Curve_NeverOvershootsBetweenSliders()
    {
        double[] gains = [0, 0, 6, 6, 0, -6, 0, 12, 12, 0];
        var tangents = EqualizerCurve.Tangents(gains);

        for (var position = 0.0; position <= gains.Length - 1; position += 0.05)
        {
            var band = Math.Min((int)position, gains.Length - 2);
            var value = EqualizerCurve.ValueAt(gains, tangents, position);
            Assert.That(value, Is.InRange(Math.Min(gains[band], gains[band + 1]) - 1e-9, Math.Max(gains[band], gains[band + 1]) + 1e-9));
        }
    }

    [Test]
    public void Curve_StaysLevelBeyondTheOuterBands()
    {
        double[] gains = [4, 0, 0, 0, -3];

        Assert.That(EqualizerCurve.DecibelsAt(EqualizerLayout.Five, gains, 20), Is.EqualTo(4));
        Assert.That(EqualizerCurve.DecibelsAt(EqualizerLayout.Five, gains, 20000), Is.EqualTo(-3));
    }

    [Test]
    public void Curve_IsSmoothBetweenBands()
    {
        double[] gains = [0, 6, 0, 0, 0];

        // Half-way between 63 and 250 Hz, a monotone cubic with a flat top at the 250 Hz slider.
        var middle = EqualizerCurve.DecibelsAt(EqualizerLayout.Five, gains, 125);

        Assert.That(middle, Is.EqualTo(3).Within(1e-9));
        Assert.That(EqualizerCurve.DecibelsAt(EqualizerLayout.Five, gains, 200), Is.GreaterThan(middle));
    }

    [Test]
    public void Resample_FromTenToFive_KeepsTheSharedBands()
    {
        double[] gains = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

        Assert.That(EqualizerCurve.Resample(gains, EqualizerLayout.Ten, EqualizerLayout.Five), Is.EqualTo(new double[] { 2, 4, 6, 8, 10 }));
    }

    [Test]
    public void Resample_ToSeven_FollowsTheCurveOnSteps()
    {
        double[] gains = [0, 0, 6, 6, 0, -6, 0, 0, 0, 0];

        var seven = EqualizerCurve.Resample(gains, EqualizerLayout.Ten, EqualizerLayout.Seven);

        Assert.That(seven, Has.Count.EqualTo(7));
        Assert.That(seven, Has.All.Matches<double>(gain => gain * 2 == Math.Round(gain * 2)));
        Assert.That(seven[0], Is.EqualTo(0));
        Assert.That(seven[3], Is.EqualTo(-6));
        Assert.That(seven[1], Is.GreaterThan(0));
    }

    [TestCase(new[] { 3.2, -12.6, 0.25, 7, double.NaN, 0, 0, 0, 0, 0 }, new[] { 3.0, -12, 0.5, 7, 0, 0, 0, 0, 0, 0 })]
    [TestCase(new[] { 1.0, 2 }, new[] { 0.0, 0, 0, 0, 0, 0, 0, 0, 0, 0 })]
    public void Normalize_KeepsWithinTheSlidersAndOnSteps(double[] requested, double[] expected)
    {
        Assert.That(EqualizerCurve.Normalize(requested, EqualizerLayout.Ten), Is.EqualTo(expected));
    }

    [Test]
    public void Normalize_WithoutACurve_IsFlat()
    {
        Assert.That(EqualizerCurve.Normalize(null, EqualizerLayout.Seven), Is.EqualTo(new double[7]));
    }

    [Test]
    public void Presets_HaveUniqueIdsAndAGainForEveryBand()
    {
        Assert.That(EqualizerPresets.All.Select(preset => preset.Id), Is.Unique);
        Assert.That(EqualizerPresets.All, Has.All.Matches<EqualizerPreset>(
            preset => preset.GainsDecibels.Count == EqualizerLayout.Default.BandCount));
        Assert.That(EqualizerPresets.Default.Id, Is.EqualTo(EqualizerPresets.DefaultId));
        Assert.That(EqualizerPresets.Default.GainsDecibels, Has.All.EqualTo(0));
        Assert.That(EqualizerPresets.All.Select(preset => preset.Id), Has.No.Member(EqualizerPresets.CustomId));
    }

    [Test]
    public void Presets_OnFiveBands_AreTheStockAndroidOnes()
    {
        // AOSP LVM kSoftPresets at 60, 230, 910, 3600 and 14000 Hz.
        Assert.That(EqualizerPresets.Find("rock").GainsFor(EqualizerLayout.Five), Is.EqualTo(new double[] { 5, 3, -1, 3, 5 }));
        Assert.That(EqualizerPresets.Find("pop").GainsFor(EqualizerLayout.Five), Is.EqualTo(new double[] { -1, 2, 5, 0.5, -2 }));
        Assert.That(EqualizerPresets.Find("jazz").GainsFor(EqualizerLayout.Five), Is.EqualTo(new double[] { 4, 2, -2, 2.5, 5 }));
    }

    [TestCase("rock", "rock")]
    [TestCase(EqualizerPresets.CustomId, EqualizerPresets.CustomId)]
    [TestCase("normal", EqualizerPresets.DefaultId)]
    [TestCase("garbage", EqualizerPresets.DefaultId)]
    public void NormalizeId_KeepsKnownPresetsAndTheCustomCurve(string id, string expected)
    {
        Assert.That(EqualizerPresets.NormalizeId(id), Is.EqualTo(expected));
    }

    [TestCase("jazz", "jazz")]
    [TestCase("no-such-preset", EqualizerPresets.DefaultId)]
    [TestCase(null, EqualizerPresets.DefaultId)]
    public void Find_FallsBackToTheDefaultPreset(string? id, string expected)
    {
        Assert.That(EqualizerPresets.Find(id).Id, Is.EqualTo(expected));
    }
}
