using Android.Media.Audiofx;
using VolumeBuster.Audio;

namespace VolumeBuster.Android.Audio;

/// <summary>
/// DynamicsProcessing on the global output mix: the input gain is the volume boost, the pre-EQ carries the
/// equalizer and the bass level (<see cref="DynamicsProcessingBands"/>); the compressor and the limiter are off.
/// </summary>
/// <remarks>
/// Above 100 % this is exactly what PipeWire and PulseAudio do: the samples are multiplied as floats and clipped
/// at full scale when converted to the output format. The effect inserts itself last in the session-0 chain,
/// after vendor effects; AudioFlinger clamps its output when converting to the HAL format (PCM 16/32 bit).
/// There is deliberately no limiter: the DynamicsProcessing limiter reacts to the RMS of whole FFT blocks,
/// not to peaks, so it lowers the gain of loud music by up to 3 dB and peaks above full scale clip anyway.
/// Measured on Nothing Phone (3a), Android 16, Bluetooth A2DP at 16/16, gain +10.57 dB, level written to the HAL:
/// a song mastered at −8.5 LUFS gets +7.7 dB (+5.0 dB with the limiter), a soundtrack at −12 LUFS +10.1 dB.
/// </remarks>
internal sealed class DynamicsProcessingEffect : ISoundEffect
{
    /// <summary>
    /// Audio session 0: the global output mix.
    /// </summary>
    private const int GlobalOutputMix = 0;

    private const int Priority = 0;
    private const int ChannelCount = 2;

    private readonly DynamicsProcessing _effect;
    private readonly IReadOnlyList<EffectBand> _bands;
    private readonly float[] _levels;

    public DynamicsProcessingEffect()
    {
        _bands = DynamicsProcessingBands.Default;
        _levels = new float[_bands.Count];
        BandFrequencies = _bands.Select(band => band.CentreFrequency).ToArray();

        var preEq = new DynamicsProcessing.Eq(true, true, _bands.Count);
        for (var band = 0; band < _bands.Count; band++)
        {
            preEq.SetBand(band, Band(band, 0));
        }

        var config = new DynamicsProcessing.Config.Builder(
                (int)VariantType.FavorFrequencyResolution,
                ChannelCount,
                true,
                _bands.Count,
                false,
                0,
                false,
                0,
                false)
            .SetPreferredFrameDuration(DynamicsProcessingBands.FrameDurationMilliseconds)!
            .SetInputGainAllChannelsTo(0f)!
            .SetPreEqAllChannelsTo(preEq)!
            .Build();

        _effect = new DynamicsProcessing(Priority, GlobalOutputMix, config);
    }

    public string Name => "Dynamics Processing";

    public bool HasControl => AndroidEffects.HasControl(_effect);

    public IReadOnlyList<double> BandFrequencies { get; }

    public void SetGain(double decibels)
    {
        _effect.SetInputGainAllChannelsTo((float)decibels);
    }

    public void SetBandLevels(IReadOnlyList<double> decibels)
    {
        if (decibels.Count != _levels.Length)
        {
            throw new ArgumentException($"Expected {_levels.Length} band levels, got {decibels.Count}.", nameof(decibels));
        }

        // Every band is a call into the audio server per channel: send only the ones that changed.
        for (var band = 0; band < _levels.Length; band++)
        {
            var level = (float)decibels[band];
            if (level.Equals(_levels[band]))
            {
                continue;
            }

            _effect.SetPreEqBandAllChannelsTo(band, Band(band, level));
            _levels[band] = level;
        }
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

    private DynamicsProcessing.EqBand Band(int band, float gainDecibels)
    {
        return new DynamicsProcessing.EqBand(true, (float)_bands[band].CutoffFrequency, gainDecibels);
    }
}
