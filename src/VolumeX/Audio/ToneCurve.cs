namespace VolumeX.Audio;

/// <summary>
/// The frequency response the sound effect applies: the equalizer curve plus the bass level.
/// </summary>
/// <remarks>
/// Both are plain gains per frequency in the DynamicsProcessing pre-EQ, raising as well as cutting, so the
/// response is exactly the curve on screen plus the bass shelf; nothing turns the rest of the sound down.
/// Raising a band raises the peaks too: above full scale they clip, as with the volume boost.
/// </remarks>
public static class ToneCurve
{
    public const double MinBassDecibels = -9;

    public const double MaxBassDecibels = 9;

    public const double BassStepDecibels = 0.5;

    public const double DefaultBassDecibels = 0;

    /// <summary>
    /// Below this the bass level applies in full.
    /// </summary>
    public const double BassFullFrequency = 50;

    /// <summary>
    /// Above this the bass level does nothing.
    /// </summary>
    public const double BassEndFrequency = 400;

    /// <summary>
    /// Clamps a bass level to the dial range and snaps it to the nearest step; garbage becomes the default.
    /// </summary>
    public static double NormalizeBass(double decibels)
    {
        if (!double.IsFinite(decibels))
        {
            return DefaultBassDecibels;
        }

        var clamped = Math.Clamp(decibels, MinBassDecibels, MaxBassDecibels);
        return Math.Round(clamped / BassStepDecibels, MidpointRounding.AwayFromZero) * BassStepDecibels;
    }

    /// <summary>
    /// Share of the bass level at <paramref name="frequency"/>: a low shelf that is full up to 50 Hz, half at
    /// about 140 Hz and gone at 400 Hz, smooth over the logarithmic frequency axis.
    /// </summary>
    public static double BassWeight(double frequency)
    {
        if (frequency <= BassFullFrequency)
        {
            return 1;
        }

        if (frequency >= BassEndFrequency)
        {
            return 0;
        }

        var x = Math.Log(frequency / BassFullFrequency) / Math.Log(BassEndFrequency / BassFullFrequency);
        return 1 - x * x * (3 - 2 * x);
    }

    /// <summary>
    /// The intended response at <paramref name="frequency"/> in decibels.
    /// </summary>
    /// <param name="frequency">
    /// Frequency in hertz.
    /// </param>
    /// <param name="layout">
    /// The layout <paramref name="equalizerGains"/> belongs to.
    /// </param>
    /// <param name="equalizerGains">
    /// Gain of every equalizer band, or null when the equalizer is off.
    /// </param>
    /// <param name="bassDecibels">
    /// The bass level, 0 when it is off.
    /// </param>
    public static double DecibelsAt(double frequency, EqualizerLayout layout, IReadOnlyList<double>? equalizerGains, double bassDecibels)
    {
        var equalizer = equalizerGains is null ? 0 : EqualizerCurve.DecibelsAt(layout, equalizerGains, frequency);
        return equalizer + NormalizeBass(bassDecibels) * BassWeight(frequency);
    }

    /// <summary>
    /// The response of <paramref name="tone"/> at every band of the effect, rounded to 0.01 dB; all zeros when
    /// neither the equalizer nor the bass level is switched on.
    /// </summary>
    public static double[] Levels(IReadOnlyList<double> bandFrequencies, ToneState tone)
    {
        var gains = tone.IsEqualizerEnabled ? tone.EqualizerGainsDecibels : null;
        var bass = tone.IsBassEnabled ? tone.BassDecibels : 0;
        return bandFrequencies
            .Select(frequency => Math.Round(DecibelsAt(frequency, tone.EqualizerLayout, gains, bass), 2))
            .ToArray();
    }
}
