using VolumeBuster.Audio;

namespace VolumeBuster.Tests.Fakes;

/// <summary>
/// A service whose every operation fails, to check the view model survives it.
/// </summary>
internal sealed class ThrowingAudioBoostService : IAudioBoostService
{
    private readonly Exception _exception;

    public ThrowingAudioBoostService(Exception exception)
    {
        _exception = exception;
    }

    public AudioBoostState State { get; } = new(AudioBoostStatus.Disabled, false, 120);

    public event EventHandler<AudioBoostState>? StateChanged
    {
        add { }
        remove { }
    }

    public Task EnableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromException(_exception);
    }

    public Task DisableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromException(_exception);
    }

    public Task SetGainAsync(double percentage, CancellationToken cancellationToken = default)
    {
        return Task.FromException(_exception);
    }

    public Task SetEqualizerEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        return Task.FromException(_exception);
    }

    public Task SetEqualizerPresetAsync(string presetId, CancellationToken cancellationToken = default)
    {
        return Task.FromException(_exception);
    }

    public Task SetEqualizerGainsAsync(IReadOnlyList<double> gainsDecibels, CancellationToken cancellationToken = default)
    {
        return Task.FromException(_exception);
    }

    public Task SetEqualizerBandCountAsync(int bandCount, CancellationToken cancellationToken = default)
    {
        return Task.FromException(_exception);
    }

    public Task SetBassEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        return Task.FromException(_exception);
    }

    public Task SetBassAsync(double decibels, CancellationToken cancellationToken = default)
    {
        return Task.FromException(_exception);
    }

    public Task DisableAllAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromException(_exception);
    }

    public Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromException(_exception);
    }

    public Task<AudioBoostState> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(State);
    }

    public void Dispose()
    {
    }
}
