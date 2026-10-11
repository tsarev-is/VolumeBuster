using Android.Media.Audiofx;
using VolumeBuster.Platform;

namespace VolumeBuster.Android.Platform;

/// <summary>
/// LoudnessEnhancer on the global output mix: it boosts the sound of every app, not only this one.
/// The effect lives as long as the app process does.
/// </summary>
public sealed class AndroidVolumeBooster : IVolumeBooster
{
    /// <summary>
    /// Audio session 0 attaches the effect to the global output mix.
    /// </summary>
    private const int GlobalOutputSession = 0;

    private readonly LoudnessEnhancer? _enhancer;

    public AndroidVolumeBooster()
    {
        try
        {
            _enhancer = new LoudnessEnhancer(GlobalOutputSession);
        }
        catch (Java.Lang.RuntimeException)
        {
            // Some firmware forbids effects on the global mix; the screen then says the device does not support it.
            _enhancer = null;
        }
    }

    public bool IsSupported => _enhancer is not null;

    public void SetGain(int millibels)
    {
        if (_enhancer is null)
        {
            return;
        }

        _enhancer.SetTargetGain(millibels);
        _enhancer.SetEnabled(millibels > 0);
    }
}
