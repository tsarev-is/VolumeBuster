using Android.Media.Audiofx;

namespace VolumeBuster.Android.Audio;

/// <summary>
/// Error handling shared by the <see cref="AudioEffect"/> wrappers.
/// </summary>
internal static class AndroidEffects
{
    /// <summary>
    /// False when another app took the effect over, the audio server dropped it or it was released.
    /// </summary>
    public static bool HasControl(AudioEffect effect)
    {
        try
        {
            return effect.HasControl;
        }
        catch (Java.Lang.IllegalStateException)
        {
            return false;
        }
    }

    /// <summary>
    /// Enables or disables the effect and turns a failure status into an exception.
    /// </summary>
    public static void SetEnabled(AudioEffect effect, bool enabled)
    {
        var status = effect.SetEnabled(enabled);
        if (status != AudioEffectStatus.Success)
        {
            throw new InvalidOperationException($"The audio server rejected {(enabled ? "enabling" : "disabling")} the effect: {status}.");
        }
    }
}
