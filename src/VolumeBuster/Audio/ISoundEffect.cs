namespace VolumeBuster.Audio;

/// <summary>
/// A platform audio effect on the global media output: a gain and, where supported, a frequency response.
/// </summary>
/// <remarks>
/// Disposing releases the effect, which removes it from the output and restores the original sound.
/// </remarks>
public interface ISoundEffect : IDisposable
{
    /// <summary>
    /// Effect name for diagnostics.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// False once another app took the effect over or the audio server restarted.
    /// </summary>
    bool HasControl { get; }

    /// <summary>
    /// Centre frequency of every band <see cref="SetBandLevels"/> takes, in hertz; empty when the effect can only
    /// change the level.
    /// </summary>
    IReadOnlyList<double> BandFrequencies { get; }

    /// <summary>
    /// Sets the gain; 0 dB is transparent.
    /// </summary>
    void SetGain(double decibels);

    /// <summary>
    /// Sets the gain of every band, raising or cutting; all zeros is transparent.
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The effect has no bands.
    /// </exception>
    void SetBandLevels(IReadOnlyList<double> decibels);

    /// <summary>
    /// Turns processing on or off without releasing the effect.
    /// </summary>
    void SetEnabled(bool enabled);
}
