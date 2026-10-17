namespace VolumeX.Audio;

/// <summary>
/// Converts the volume percentage shown to the user into the gain applied to the audio signal.
/// </summary>
/// <remarks>
/// Percentages follow the cubic volume scale of PulseAudio and PipeWire, so "150 %" here equals
/// "150 %" in the volume slider of a Linux desktop: amplitude = (percent / 100)³, i.e. +10.6 dB.
/// The value is a signal gain, not perceived loudness or acoustic output.
/// </remarks>
public static class GainCalculator
{
    /// <summary>
    /// The system maximum: no extra gain.
    /// </summary>
    public const double MinPercent = 100;

    /// <summary>
    /// The upper end of the slider, same as the over-amplification limit of GNOME and KDE.
    /// </summary>
    public const double MaxPercent = 150;

    /// <summary>
    /// Slider step.
    /// </summary>
    public const double StepPercent = 5;

    /// <summary>
    /// The value a fresh install starts with.
    /// </summary>
    public const double DefaultPercent = MinPercent;

    /// <summary>
    /// Linear amplitude factor for a volume percentage on the cubic scale.
    /// </summary>
    public static double PercentToAmplitude(double percent)
    {
        var ratio = percent / 100.0;
        return ratio * ratio * ratio;
    }

    /// <summary>
    /// Gain in decibels: 20·log10(amplitude) = 60·log10(percent / 100).
    /// </summary>
    public static double PercentToDecibels(double percent)
    {
        return 60.0 * Math.Log10(percent / 100.0);
    }

    /// <summary>
    /// Millibels (1/100 dB), the unit of Android's LoudnessEnhancer.
    /// </summary>
    public static int DecibelsToMillibels(double decibels)
    {
        return (int)Math.Round(decibels * 100.0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Millibels for a volume percentage.
    /// </summary>
    public static int PercentToMillibels(double percent)
    {
        return DecibelsToMillibels(PercentToDecibels(percent));
    }

    /// <summary>
    /// Clamps a percentage to the slider range and snaps it to the nearest step; garbage becomes the minimum.
    /// </summary>
    public static double Normalize(double percent)
    {
        if (double.IsNaN(percent) || double.IsInfinity(percent))
        {
            return MinPercent;
        }

        var clamped = Math.Clamp(percent, MinPercent, MaxPercent);
        var steps = Math.Round((clamped - MinPercent) / StepPercent, MidpointRounding.AwayFromZero);
        return MinPercent + steps * StepPercent;
    }
}
