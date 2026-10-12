using VolumeBuster.Audio;

namespace VolumeBuster.Tests.Fakes;

/// <summary>
/// Records what the engine does to the platform effect; its bands sit at the ten-band equalizer frequencies,
/// so the levels of a ten-band curve are the curve itself.
/// </summary>
internal sealed class FakeSoundEffect : ISoundEffect
{
    public string Name => "Fake effect";

    public bool HasControl { get; set; } = true;

    public IReadOnlyList<double> BandFrequencies { get; set; } = EqualizerLayout.Ten.Frequencies;

    public bool IsEnabled { get; private set; }

    public bool IsDisposed { get; private set; }

    public List<double> AppliedGains { get; } = [];

    public double Gain => AppliedGains.Count > 0 ? AppliedGains[^1] : 0;

    /// <summary>
    /// Every set of band levels applied, oldest first.
    /// </summary>
    public List<IReadOnlyList<double>> AppliedLevels { get; } = [];

    public IReadOnlyList<double> BandLevels => AppliedLevels.Count > 0 ? AppliedLevels[^1] : new double[BandFrequencies.Count];

    /// <summary>
    /// The order of everything the engine did, e.g. "gain 3.5", "levels max 4".
    /// </summary>
    public List<string> Journal { get; } = [];

    /// <summary>
    /// Thrown by <see cref="SetGain"/>, e.g. a dead audio server.
    /// </summary>
    public Exception? SetGainException { get; set; }

    /// <summary>
    /// Thrown when the effect is enabled.
    /// </summary>
    public Exception? EnableException { get; set; }

    public void SetGain(double decibels)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (SetGainException is not null)
        {
            throw SetGainException;
        }

        AppliedGains.Add(decibels);
        Journal.Add($"gain {decibels:0.##}");
    }

    public void SetBandLevels(IReadOnlyList<double> decibels)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (BandFrequencies.Count == 0)
        {
            throw new NotSupportedException("No bands.");
        }

        Assert.That(decibels, Has.Count.EqualTo(BandFrequencies.Count));
        AppliedLevels.Add(decibels.ToArray());
        Journal.Add($"levels max {decibels.Max():0.##}");
    }

    public void SetEnabled(bool enabled)
    {
        ObjectDisposedException.ThrowIf(IsDisposed, this);
        if (enabled && EnableException is not null)
        {
            throw EnableException;
        }

        IsEnabled = enabled;
    }

    public void Dispose()
    {
        IsEnabled = false;
        IsDisposed = true;
        Journal.Add("released");
    }
}
