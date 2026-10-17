using Android.Media.Audiofx;
using VolumeX.Audio;

namespace VolumeX.Android.Audio;

/// <summary>
/// LoudnessEnhancer on the global output mix, the fallback for devices without DynamicsProcessing: it changes
/// only the level, so the equalizer and the bass level are unavailable with it.
/// </summary>
/// <remarks>
/// It is a compressor, not a plain gain: quiet input gets the full target gain, loud input is pulled down
/// toward a knee that drops as the gain grows, so loud music gains less than with DynamicsProcessing.
/// </remarks>
internal sealed class LoudnessEnhancerEffect : ISoundEffect
{
    /// <summary>
    /// Audio session 0: the global output mix.
    /// </summary>
    private const int GlobalOutputMix = 0;

    private readonly LoudnessEnhancer _effect;

    public LoudnessEnhancerEffect()
    {
        _effect = new LoudnessEnhancer(GlobalOutputMix);
        _effect.SetTargetGain(0);
    }

    public string Name => "Loudness Enhancer";

    public bool HasControl => AndroidEffects.HasControl(_effect);

    public IReadOnlyList<double> BandFrequencies => [];

    public void SetGain(double decibels)
    {
        _effect.SetTargetGain(GainCalculator.DecibelsToMillibels(decibels));
    }

    public void SetBandLevels(IReadOnlyList<double> decibels)
    {
        throw new NotSupportedException("LoudnessEnhancer changes only the level.");
    }

    public void SetEnabled(bool enabled)
    {
        AndroidEffects.SetEnabled(_effect, enabled);
    }

    public void Dispose()
    {
        _effect.Release();
        _effect.Dispose();
    }
}
