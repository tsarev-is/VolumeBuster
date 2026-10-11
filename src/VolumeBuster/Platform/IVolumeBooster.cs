namespace VolumeBuster.Platform;

/// <summary>
/// Raises the loudness of everything the device plays above the system maximum.
/// </summary>
public interface IVolumeBooster
{
    /// <summary>
    /// False when the device refuses the effect on the global audio output.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Sets the extra gain in millibels (100 mB = 1 dB); 0 turns the boost off.
    /// </summary>
    void SetGain(int millibels);
}
