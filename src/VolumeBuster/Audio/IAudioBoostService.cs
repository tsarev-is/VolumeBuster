namespace VolumeBuster.Audio;

/// <summary>
/// Changes every app's sound: gain on top of the system media volume, an equalizer and a bass level,
/// each switched on and off on its own.
/// </summary>
public interface IAudioBoostService : IDisposable
{
    /// <summary>
    /// Current state; never claims Active unless the platform accepted and handed over the effect.
    /// </summary>
    AudioBoostState State { get; }

    /// <summary>
    /// Raised after every state change, possibly on a background thread.
    /// </summary>
    event EventHandler<AudioBoostState>? StateChanged;

    /// <summary>
    /// Switches the volume boost on: attaches the gain effect and ramps up to the selected gain.
    /// </summary>
    Task EnableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the volume boost off; the equalizer and the bass level keep working. The system volume is not touched.
    /// </summary>
    Task DisableAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects the volume percentage; it is clamped and snapped to the slider steps.
    /// </summary>
    Task SetGainAsync(double percentage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the equalizer on or off; the volume boost and the bass level are not affected.
    /// </summary>
    Task SetEqualizerEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects an equalizer preset or, with <see cref="EqualizerPresets.CustomId"/>, the user's own curve;
    /// unknown identifiers select the default preset.
    /// </summary>
    Task SetEqualizerPresetAsync(string presetId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sets the user's own equalizer curve, a gain for every reference frequency, and selects it.
    /// </summary>
    Task SetEqualizerGainsAsync(IReadOnlyList<double> gainsDecibels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects 5, 7 or 10 band sliders; the current curve is carried over to the new bands.
    /// </summary>
    Task SetEqualizerBandCountAsync(int bandCount, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches the bass level on or off; the volume boost and the equalizer are not affected.
    /// </summary>
    Task SetBassEnabledAsync(bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Selects the bass level in decibels, negative to cut the bass; it is clamped and snapped to the slider steps.
    /// </summary>
    Task SetBassAsync(double decibels, CancellationToken cancellationToken = default);

    /// <summary>
    /// Switches everything off and releases every effect.
    /// </summary>
    Task DisableAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Re-applies the saved preferences: switches on whatever the user left switched on.
    /// </summary>
    Task RestoreAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Same as <see cref="State"/>, for callers that prefer the async form.
    /// </summary>
    Task<AudioBoostState> GetStatusAsync(CancellationToken cancellationToken = default);
}
