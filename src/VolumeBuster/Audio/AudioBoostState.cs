namespace VolumeBuster.Audio;

/// <summary>
/// Snapshot of the audio engine.
/// </summary>
/// <param name="Status">
/// What the volume boost actually does.
/// </param>
/// <param name="IsEnabled">
/// The volume boost is switched on: true while active and while an error is being retried.
/// </param>
/// <param name="GainPercent">
/// Selected volume percentage, see <see cref="GainCalculator"/>.
/// </param>
/// <param name="EffectName">
/// The platform effect in use, if any.
/// </param>
/// <param name="Message">
/// Why the engine is unsupported or failed; null otherwise.
/// </param>
public sealed record AudioBoostState(
    AudioBoostStatus Status,
    bool IsEnabled,
    double GainPercent,
    string? EffectName = null,
    string? Message = null)
{
    /// <summary>
    /// The equalizer and the bass level.
    /// </summary>
    public ToneState Tone { get; init; } = ToneState.Off;

    /// <summary>
    /// Selected gain in decibels.
    /// </summary>
    public double GainDecibels => GainCalculator.PercentToDecibels(GainPercent);

    /// <summary>
    /// The boost, the equalizer or the bass level is switched on: the app is changing the sound.
    /// </summary>
    public bool IsAnyEnabled => IsEnabled || Tone.IsEnabled;
}
