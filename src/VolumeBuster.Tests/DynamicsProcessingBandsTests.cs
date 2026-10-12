using VolumeBuster.Audio;

namespace VolumeBuster.Tests;

public sealed class DynamicsProcessingBandsTests
{
    [TestCase(48000, 10, 512)]
    [TestCase(48000, 40, 2048)]
    [TestCase(44100, 40, 2048)]
    [TestCase(48000, 80, 4096)]
    public void BlockSize_IsTheFrameRoundedUpToAPowerOfTwo(int sampleRate, double frameMilliseconds, int expected)
    {
        Assert.That(DynamicsProcessingBands.BlockSize(sampleRate, frameMilliseconds), Is.EqualTo(expected));
    }

    [Test]
    public void Default_FitsAndCoversTheWholeSpectrum()
    {
        var bands = DynamicsProcessingBands.Default;

        Assert.That(bands, Has.Count.InRange(32, DynamicsProcessingBands.MaxBands));
        Assert.That(bands.Select(band => band.CutoffFrequency), Is.Ordered.Ascending);
        Assert.That(bands[^1].CutoffFrequency, Is.EqualTo(24000));
        Assert.That(bands, Has.All.Matches<EffectBand>(band => band.CentreFrequency > 0 && band.CentreFrequency <= band.CutoffFrequency + 12));
    }

    [Test]
    public void LowBands_EachCoverOneBinAndAreCentredOnIt()
    {
        var bands = DynamicsProcessingBands.Default;
        const double binWidth = 48000.0 / 2048;

        // DPFrequency: a band covers the bins up to round(cutoff / binWidth).
        for (var bin = 1; bin <= 10; bin++)
        {
            Assert.That((int)(0.5 + bands[bin].CutoffFrequency / binWidth), Is.EqualTo(bin));
            Assert.That(bands[bin].CentreFrequency, Is.EqualTo(bin * binWidth).Within(1e-9));
        }
    }

    [Test]
    public void LowBands_KeepTheTenBandEqualizerApart()
    {
        // 31 and 63 Hz must land in different bins, which the default 10 ms frame (94 Hz bins) cannot do.
        var bands = DynamicsProcessingBands.Default;
        var at31 = bands.First(band => band.CutoffFrequency >= 31.25 - 23.4375 / 2);
        var at63 = bands.First(band => band.CutoffFrequency >= 62.5 - 23.4375 / 2);

        Assert.That(at31, Is.Not.SameAs(at63));
    }

    [Test]
    public void HighBands_AreAnEighthOfAnOctaveWide()
    {
        var bands = DynamicsProcessingBands.Default;
        var high = bands.Where(band => band.CutoffFrequency is > 400 and < 20000).ToArray();

        for (var i = 1; i < high.Length; i++)
        {
            Assert.That(high[i].CutoffFrequency / high[i - 1].CutoffFrequency, Is.EqualTo(Math.Pow(2, 1.0 / 8)).Within(1e-9));
        }
    }
}
