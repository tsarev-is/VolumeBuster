namespace VolumeX.Audio;

/// <summary>
/// The band layout for the pre-EQ of Android's DynamicsProcessing, which carries the equalizer and the bass level.
/// </summary>
/// <remarks>
/// The AOSP implementation (<c>DPFrequency</c>) is an FFT equalizer with a constant gain per bin: bin k takes the
/// gain of the first band whose cutoff rounds to bin k or higher. The bin width is the sample rate over the block
/// size, and the block is the preferred frame duration rounded up to a power of two: the default 10 ms gives
/// 94 Hz bins, too coarse for 31 and 63 Hz. At 40 ms the block is 2048 samples, 23 Hz bins at 48 kHz, which keeps
/// 31 Hz and 63 Hz apart; it delays the sound by about 21 ms (measured: +21.4 ms against +5.4 ms at 10 ms).
/// Below about 250 Hz every bin gets a band of its own, centred on the bin, so the response is not shifted by half
/// a bin; above that the bands are an eighth of an octave wide. Measured on the emulator, the result follows the
/// intended curve within 0.2 dB from 25 Hz to 16 kHz for all three layouts and the bass shelf.
/// </remarks>
public static class DynamicsProcessingBands
{
    /// <summary>
    /// The preferred frame duration requested from DynamicsProcessing.
    /// </summary>
    public const float FrameDurationMilliseconds = 40;

    /// <summary>
    /// The mixer rate the layout is built for; at 44.1 kHz the low bins sit 8 % lower, which is harmless.
    /// </summary>
    public const int SampleRate = 48000;

    public const int BandsPerOctave = 8;

    /// <summary>
    /// Most bands Android accepts in one stage (the AIDL effect's capability).
    /// </summary>
    public const int MaxBands = 128;

    /// <summary>
    /// The layout for <see cref="FrameDurationMilliseconds"/> at <see cref="SampleRate"/>.
    /// </summary>
    public static IReadOnlyList<EffectBand> Default { get; } =
        Create(SampleRate, BlockSize(SampleRate, FrameDurationMilliseconds), BandsPerOctave);

    /// <summary>
    /// The FFT block DynamicsProcessing uses: the frame in samples rounded up to a power of two.
    /// </summary>
    public static int BlockSize(int sampleRate, double frameDurationMilliseconds)
    {
        var samples = (int)(frameDurationMilliseconds * sampleRate / 1000);
        return (int)System.Numerics.BitOperations.RoundUpToPowerOf2((uint)Math.Max(samples, 8));
    }

    /// <summary>
    /// A band per bin while bins are wider than <paramref name="bandsPerOctave"/>, then bands of that width up
    /// to the Nyquist frequency.
    /// </summary>
    public static IReadOnlyList<EffectBand> Create(int sampleRate, int blockSize, int bandsPerOctave)
    {
        var binWidth = (double)sampleRate / blockSize;
        var nyquist = sampleRate / 2.0;
        var ratio = Math.Pow(2, 1.0 / bandsPerOctave);
        var bands = new List<EffectBand>();

        // A cutoff exactly on bin k rounds to k, so each of these bands covers exactly one bin.
        var bin = 0;
        while (bin == 0 || (bin + 0.5) * binWidth * (ratio - 1) < binWidth)
        {
            bands.Add(new EffectBand(bin * binWidth, Math.Max(bin, 0.5) * binWidth));
            bin++;
        }

        var lower = (bin - 0.5) * binWidth;
        while (lower < nyquist)
        {
            var upper = Math.Min(lower * ratio, nyquist);
            bands.Add(new EffectBand(upper, Math.Sqrt(lower * upper)));
            lower = upper;
        }

        return bands;
    }
}
