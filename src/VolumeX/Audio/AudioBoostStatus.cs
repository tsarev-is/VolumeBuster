namespace VolumeX.Audio;

/// <summary>
/// What the audio engine is actually doing, as opposed to what the user asked for.
/// </summary>
public enum AudioBoostStatus
{
    /// <summary>
    /// No effect is attached; the sound is exactly what Android produces.
    /// </summary>
    Disabled,

    /// <summary>
    /// The effect is attached to the global media output, enabled and controlled by this app.
    /// </summary>
    Active,

    /// <summary>
    /// The device has no suitable effect or refuses effects on the global output.
    /// </summary>
    Unsupported,

    /// <summary>
    /// The effect failed or another app took it over.
    /// </summary>
    Error,
}
