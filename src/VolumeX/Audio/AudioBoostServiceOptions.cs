namespace VolumeX.Audio;

/// <summary>
/// Timing of <see cref="AudioBoostService"/>.
/// </summary>
public sealed record AudioBoostServiceOptions
{
    /// <summary>
    /// Largest gain increase applied at once.
    /// </summary>
    public double RampStepDecibels { get; init; } = 0.5;

    /// <summary>
    /// Pause between ramp steps: 0.5 dB per 15 ms reaches +10.6 dB in about a third of a second.
    /// </summary>
    public TimeSpan RampStepDelay { get; init; } = TimeSpan.FromMilliseconds(15);

    /// <summary>
    /// Largest rise of any equalizer band applied at once; a preset or the bass level comes in over a few steps.
    /// </summary>
    public double ToneRampStepDecibels { get; init; } = 2;

    /// <summary>
    /// How often an enabled boost checks that its effect is still alive; null turns the check off.
    /// </summary>
    public TimeSpan? HealthCheckInterval { get; init; } = TimeSpan.FromSeconds(5);
}
