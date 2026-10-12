namespace VolumeBuster.Audio;

/// <summary>
/// Creates the platform sound effect.
/// </summary>
public interface ISoundEffectFactory
{
    /// <summary>
    /// Creates the effect on the global media output: disabled, transparent and controlled by this app.
    /// </summary>
    /// <exception cref="EffectUnavailableException">
    /// The device has no suitable effect or does not allow it on the global output.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The effect exists but another app controls it.
    /// </exception>
    ISoundEffect Create();
}
