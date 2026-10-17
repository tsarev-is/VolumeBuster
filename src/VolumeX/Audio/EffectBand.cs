namespace VolumeX.Audio;

/// <summary>
/// One band of an FFT equalizer: it covers the bins from the previous band's cutoff up to its own.
/// </summary>
/// <param name="CutoffFrequency">
/// Upper edge in hertz.
/// </param>
/// <param name="CentreFrequency">
/// The frequency whose response the band gets, in hertz.
/// </param>
public sealed record EffectBand(double CutoffFrequency, double CentreFrequency);
