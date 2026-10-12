namespace VolumeBuster.Audio;

/// <summary>
/// The continuous equalizer curve through the band sliders: what is drawn is exactly what is heard.
/// </summary>
/// <remarks>
/// A monotone cubic (Fritsch–Butland) over the logarithmic frequency axis: it passes through every slider, is
/// smooth, and never overshoots between two of them, so a +6 dB slider next to a 0 dB one never sounds like +7 dB.
/// Beyond the outer bands the curve stays at their level, like the shelving outer bands of a graphic equalizer.
/// </remarks>
public static class EqualizerCurve
{
    /// <summary>
    /// Lowest gain of a band slider.
    /// </summary>
    public const double MinGainDecibels = -12;

    /// <summary>
    /// Highest gain of a band slider.
    /// </summary>
    public const double MaxGainDecibels = 12;

    public const double GainStepDecibels = 0.5;

    /// <summary>
    /// Slope of the curve at every slider, in decibels per band: zero at the outer bands and wherever the curve
    /// turns, the harmonic mean of the neighbouring slopes elsewhere.
    /// </summary>
    public static double[] Tangents(IReadOnlyList<double> gains)
    {
        var tangents = new double[gains.Count];
        for (var band = 1; band < gains.Count - 1; band++)
        {
            var before = gains[band] - gains[band - 1];
            var after = gains[band + 1] - gains[band];
            if (before * after > 0)
            {
                tangents[band] = 2 / (1 / before + 1 / after);
            }
        }

        return tangents;
    }

    /// <summary>
    /// The curve at <paramref name="position"/> in band units (0 at the first slider); constant beyond the ends.
    /// </summary>
    public static double ValueAt(IReadOnlyList<double> gains, IReadOnlyList<double> tangents, double position)
    {
        if (gains.Count == 0)
        {
            return 0;
        }

        if (position <= 0)
        {
            return gains[0];
        }

        if (position >= gains.Count - 1)
        {
            return gains[^1];
        }

        var band = (int)position;
        var t = position - band;
        var t2 = t * t;
        var t3 = t2 * t;
        return (2 * t3 - 3 * t2 + 1) * gains[band]
            + (t3 - 2 * t2 + t) * tangents[band]
            + (-2 * t3 + 3 * t2) * gains[band + 1]
            + (t3 - t2) * tangents[band + 1];
    }

    /// <summary>
    /// The curve at <paramref name="frequency"/> in hertz.
    /// </summary>
    public static double DecibelsAt(EqualizerLayout layout, IReadOnlyList<double> gains, double frequency)
    {
        return ValueAt(gains, Tangents(gains), layout.PositionOf(frequency));
    }

    /// <summary>
    /// The same curve on another layout: read at the new band frequencies and snapped to slider steps.
    /// </summary>
    public static IReadOnlyList<double> Resample(IReadOnlyList<double> gains, EqualizerLayout from, EqualizerLayout to)
    {
        var tangents = Tangents(gains);
        return to.Frequencies
            .Select(frequency => Snap(ValueAt(gains, tangents, from.PositionOf(frequency))))
            .ToArray();
    }

    /// <summary>
    /// A gain for every band of <paramref name="layout"/>, each within the slider range and on a step.
    /// A curve saved for another layout is resampled; a missing or unrecognisable one becomes flat.
    /// </summary>
    public static IReadOnlyList<double> Normalize(IReadOnlyList<double>? gains, EqualizerLayout layout)
    {
        if (gains is null)
        {
            return new double[layout.BandCount];
        }

        var clean = gains.Select(gain => double.IsFinite(gain) ? Snap(gain) : 0).ToArray();
        if (clean.Length == layout.BandCount)
        {
            return clean;
        }

        var source = EqualizerLayout.All.FirstOrDefault(candidate => candidate.BandCount == clean.Length);
        return source is null ? new double[layout.BandCount] : Resample(clean, source, layout);
    }

    private static double Snap(double gain)
    {
        var clamped = Math.Clamp(gain, MinGainDecibels, MaxGainDecibels);
        return Math.Round(clamped / GainStepDecibels, MidpointRounding.AwayFromZero) * GainStepDecibels;
    }
}
