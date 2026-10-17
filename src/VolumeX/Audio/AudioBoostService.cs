using System.Globalization;
using Microsoft.Extensions.Logging;
using VolumeX.Localization;
using VolumeX.Settings;

namespace VolumeX.Audio;

/// <summary>
/// Platform-independent sound engine: the volume boost, the equalizer and the bass level, each switched on its own.
/// It owns the effect, brings every rise in gradually, remembers the user's choice and re-creates the effect
/// if the audio server drops it.
/// </summary>
/// <remarks>
/// All three run in one effect on the global output: the boost is its gain, the equalizer and the bass level are
/// its frequency response (see <see cref="ToneCurve"/>). The effect is attached while anything is switched on and
/// processes only while it changes the sound. <see cref="StateChanged"/> handlers run while the engine is busy:
/// they must not block on it.
/// </remarks>
public sealed class AudioBoostService : IAudioBoostService
{
    private readonly ISoundEffectFactory _effectFactory;
    private readonly ISettingsStore _settingsStore;
    private readonly AudioBoostServiceOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AudioBoostService> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();

    private ISoundEffect? _effect;
    private double _appliedGain;
    private double[] _appliedLevels = [];
    private bool _isEffectEnabled;
    private CancellationTokenSource? _healthCheck;
    private AudioBoostState _state;
    private bool _disposed;

    public AudioBoostService(
        ISoundEffectFactory effectFactory,
        ISettingsStore settingsStore,
        AudioBoostServiceOptions options,
        TimeProvider timeProvider,
        ILogger<AudioBoostService> logger)
    {
        _effectFactory = effectFactory;
        _settingsStore = settingsStore;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;

        var settings = settingsStore.Load();
        var layout = EqualizerLayout.For(settings.EqualizerBandCount);
        var presetId = EqualizerPresets.NormalizeId(settings.EqualizerPresetId);
        _state = new AudioBoostState(AudioBoostStatus.Disabled, false, GainCalculator.Normalize(settings.GainPercent))
        {
            Tone = ToneState.Off with
            {
                EqualizerLayout = layout,
                EqualizerPresetId = presetId,
                EqualizerGainsDecibels = GainsFor(presetId, layout, settings),
                BassDecibels = ToneCurve.NormalizeBass(settings.BassDecibels),
            },
        };
    }

    public AudioBoostState State => Volatile.Read(ref _state);

    public event EventHandler<AudioBoostState>? StateChanged;

    public async Task EnableAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_state.IsEnabled && _effect is not null)
            {
                return;
            }

            await ApplyAsync(_state with { IsEnabled = true }, isRecovery: false, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisableAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            // Lowering is immediate, so a cancelled caller cannot leave the gain half-way.
            var next = _state with { Status = AudioBoostStatus.Disabled, IsEnabled = false, EffectName = null, Message = null };
            await ApplyAsync(next, isRecovery: false, CancellationToken.None);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetGainAsync(double percentage, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var percent = GainCalculator.Normalize(percentage);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (percent.Equals(_state.GainPercent))
            {
                return;
            }

            SaveSettings(settings => settings with { GainPercent = percent });
            var next = _state with { GainPercent = percent };
            if (next.IsEnabled && _effect is not null)
            {
                await ApplyAsync(next, isRecovery: false, cancellationToken);
            }
            else
            {
                Publish(next);
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetEqualizerEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (enabled == _state.Tone.IsEqualizerEnabled && (!enabled || _effect is not null))
            {
                return;
            }

            await ApplyAsync(_state with { Tone = _state.Tone with { IsEqualizerEnabled = enabled } }, isRecovery: false, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetEqualizerPresetAsync(string presetId, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var id = EqualizerPresets.NormalizeId(presetId);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (id == _state.Tone.EqualizerPresetId)
            {
                return;
            }

            SaveSettings(settings => settings with { EqualizerPresetId = id });
            var gains = GainsFor(id, _state.Tone.EqualizerLayout, _settingsStore.Load());
            await ApplyToneChangeAsync(_state.Tone with { EqualizerPresetId = id, EqualizerGainsDecibels = gains }, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetEqualizerGainsAsync(IReadOnlyList<double> gainsDecibels, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            // A curve for another layout (the band count changed while it was on its way) is carried over.
            var gains = EqualizerCurve.Normalize(gainsDecibels, _state.Tone.EqualizerLayout);
            if (_state.Tone.EqualizerPresetId == EqualizerPresets.CustomId && gains.SequenceEqual(_state.Tone.EqualizerGainsDecibels))
            {
                return;
            }

            SaveSettings(settings => settings with { EqualizerPresetId = EqualizerPresets.CustomId, CustomEqualizerGainsDecibels = gains });
            await ApplyToneChangeAsync(_state.Tone with { EqualizerPresetId = EqualizerPresets.CustomId, EqualizerGainsDecibels = gains }, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetEqualizerBandCountAsync(int bandCount, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var layout = EqualizerLayout.For(bandCount);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            var tone = _state.Tone;
            if (layout == tone.EqualizerLayout)
            {
                return;
            }

            // A preset is defined for every layout; the user's own curve keeps its shape on the new bands.
            var isCustom = tone.EqualizerPresetId == EqualizerPresets.CustomId;
            var gains = isCustom
                ? EqualizerCurve.Resample(tone.EqualizerGainsDecibels, tone.EqualizerLayout, layout)
                : EqualizerPresets.Find(tone.EqualizerPresetId).GainsFor(layout);
            SaveSettings(settings => settings with
            {
                EqualizerBandCount = layout.BandCount,
                CustomEqualizerGainsDecibels = isCustom ? gains : settings.CustomEqualizerGainsDecibels,
            });
            await ApplyToneChangeAsync(tone with { EqualizerLayout = layout, EqualizerGainsDecibels = gains }, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetBassEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (enabled == _state.Tone.IsBassEnabled && (!enabled || _effect is not null))
            {
                return;
            }

            await ApplyAsync(_state with { Tone = _state.Tone with { IsBassEnabled = enabled } }, isRecovery: false, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task SetBassAsync(double decibels, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var level = ToneCurve.NormalizeBass(decibels);

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (level.Equals(_state.Tone.BassDecibels))
            {
                return;
            }

            SaveSettings(settings => settings with { BassDecibels = level });
            await ApplyToneChangeAsync(_state.Tone with { BassDecibels = level }, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DisableAllAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            StopHealthCheck();
            ReleaseEffect();
            SaveSettings(settings => settings with { IsEnabled = false, IsEqualizerEnabled = false, IsBassEnabled = false });
            Publish(_state with
            {
                Status = AudioBoostStatus.Disabled,
                IsEnabled = false,
                EffectName = null,
                Message = null,
                Tone = _state.Tone with
                {
                    Status = AudioBoostStatus.Disabled,
                    IsEqualizerEnabled = false,
                    IsBassEnabled = false,
                    EffectName = null,
                    Message = null,
                },
            });
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task RestoreAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var settings = _settingsStore.Load();
        if (!settings.IsEnabled && !settings.IsEqualizerEnabled && !settings.IsBassEnabled)
        {
            return;
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_effect is not null)
            {
                return;
            }

            var next = _state with
            {
                IsEnabled = settings.IsEnabled,
                Tone = _state.Tone with { IsEqualizerEnabled = settings.IsEqualizerEnabled, IsBassEnabled = settings.IsBassEnabled },
            };
            await ApplyAsync(next, isRecovery: false, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public Task<AudioBoostState> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(State);
    }

    /// <summary>
    /// Checks that the effect is still controlled by this app while anything is switched on and re-creates it
    /// otherwise, e.g. after the audio server restarted or another app took the effect over.
    /// </summary>
    public async Task VerifyAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!_state.IsAnyEnabled || _effect is { HasControl: true })
            {
                return;
            }

            _logger.LogWarning(_effect is null ? "Retrying to attach the sound effect." : $"Lost control of {_effect.Name}; re-creating it.");
            ReleaseEffect();
            await ApplyAsync(_state, isRecovery: true, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _lifetime.Cancel();

        // An operation in flight may still hold the effect; give it a moment, then release regardless.
        var entered = _gate.Wait(TimeSpan.FromSeconds(1));
        try
        {
            StopHealthCheck();
            ReleaseEffect();
        }
        finally
        {
            if (entered)
            {
                _gate.Release();
            }

            _lifetime.Dispose();
        }
    }

    /// <summary>
    /// Brings the effect to <paramref name="requested"/>: attaches it when anything is on, releases it when
    /// nothing is, applies the frequency response and the gain, and publishes what actually happened.
    /// </summary>
    /// <param name="requested">
    /// The state the user asked for.
    /// </param>
    /// <param name="isRecovery">
    /// Re-creating a lost effect: failures keep everything switched on so the health check retries, and the
    /// saved switches are left alone.
    /// </param>
    /// <param name="cancellationToken">
    /// Stops the gradual rise; the effect is then released.
    /// </param>
    private async Task ApplyAsync(AudioBoostState requested, bool isRecovery, CancellationToken cancellationToken)
    {
        if (!requested.IsAnyEnabled)
        {
            ReleaseEffect();
            Commit(Settle(requested, effectName: null, toneProblem: null), isRecovery);
            return;
        }

        ISoundEffect effect;
        try
        {
            effect = _effect ??= CreateEffect();
        }
        catch (EffectUnavailableException exception) when (!isRecovery)
        {
            _logger.LogWarning(exception, "The device does not allow a sound effect on the global output.");
            Commit(Failed(requested, AudioBoostStatus.Unsupported, exception.Message, isRecovery), isRecovery);
            return;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, isRecovery ? "Re-creating the sound effect failed; will retry." : "Attaching the sound effect failed.");
            Commit(Failed(requested, AudioBoostStatus.Error, exception.Message, isRecovery), isRecovery);
            return;
        }

        var toneProblem = requested.Tone.IsEnabled && effect.BandFrequencies.Count == 0
            ? string.Format(CultureInfo.CurrentCulture, Strings.ToneUnsupportedFormat, effect.Name)
            : null;
        var settled = Settle(requested, effect.Name, toneProblem);
        if (!settled.IsAnyEnabled)
        {
            ReleaseEffect();
            Commit(settled, isRecovery);
            return;
        }

        var levels = settled.Tone.IsEnabled
            ? ToneCurve.Levels(effect.BandFrequencies, settled.Tone)
            : new double[effect.BandFrequencies.Count];
        var gain = settled.IsEnabled ? settled.GainDecibels : 0;

        try
        {
            // Quieter first; every rise comes in over small steps, so the sound never jumps up.
            if (gain < _appliedGain)
            {
                ApplyGain(gain);
            }

            await RampLevelsAsync(levels, cancellationToken);
            await RampGainAsync(gain, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            StopHealthCheck();
            ReleaseEffect();
            Publish(Settle(requested with { IsEnabled = false, Tone = requested.Tone with { IsEqualizerEnabled = false, IsBassEnabled = false } }, null, null));
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Applying the sound effect failed; it is released and will be re-created.");
            ReleaseEffect();
            Commit(Failed(settled, AudioBoostStatus.Error, exception.Message, isRecovery), isRecovery);
            return;
        }

        if (settled.Tone.IsEnabled)
        {
            _logger.LogInformation($"Frequency response: {Describe(levels, effect.BandFrequencies)}.");
        }

        Commit(settled, isRecovery);
    }

    /// <summary>
    /// Re-applies a changed equalizer curve or bass level while the effect is shaping the sound; otherwise only
    /// remembers it.
    /// </summary>
    private async Task ApplyToneChangeAsync(ToneState tone, CancellationToken cancellationToken)
    {
        var next = _state with { Tone = tone };
        if (tone.IsEnabled && _effect is not null)
        {
            await ApplyAsync(next, isRecovery: false, cancellationToken);
        }
        else
        {
            Publish(next);
        }
    }

    private ISoundEffect CreateEffect()
    {
        var effect = _effectFactory.Create();
        _appliedGain = 0;
        _appliedLevels = new double[effect.BandFrequencies.Count];
        _isEffectEnabled = false;
        _logger.LogInformation($"Attached {effect.Name} to the global output with {effect.BandFrequencies.Count} bands.");
        return effect;
    }

    /// <summary>
    /// <paramref name="requested"/> as it is once applied: what is on is active, what is off is idle; a frequency
    /// response the effect cannot make switches the equalizer and the bass level off with
    /// <paramref name="toneProblem"/>.
    /// </summary>
    private static AudioBoostState Settle(AudioBoostState requested, string? effectName, string? toneProblem)
    {
        var boost = requested.IsEnabled
            ? requested with { Status = AudioBoostStatus.Active, EffectName = effectName, Message = null }
            : requested with { Status = Idle(requested.Status), EffectName = null };

        var tone = requested.Tone;
        if (toneProblem is not null)
        {
            tone = tone with { Status = AudioBoostStatus.Unsupported, IsEqualizerEnabled = false, IsBassEnabled = false, EffectName = null, Message = toneProblem };
        }
        else
        {
            tone = tone.IsEnabled
                ? tone with { Status = AudioBoostStatus.Active, EffectName = effectName, Message = null }
                : tone with { Status = Idle(tone.Status), EffectName = null };
        }

        return boost with { Tone = tone };
    }

    /// <summary>
    /// What a switched-off feature reports: nothing, or the problem from its last attempt.
    /// </summary>
    private static AudioBoostStatus Idle(AudioBoostStatus status)
    {
        return status == AudioBoostStatus.Active ? AudioBoostStatus.Disabled : status;
    }

    /// <summary>
    /// <paramref name="requested"/> after the effect failed. What was already on stays on and is retried by the
    /// health check, unless the device does not support the effect at all; what was just being switched on
    /// flips back off.
    /// </summary>
    private AudioBoostState Failed(AudioBoostState requested, AudioBoostStatus status, string message, bool isRecovery)
    {
        var previous = _state;
        var canRetry = status != AudioBoostStatus.Unsupported;

        var result = requested.IsEnabled
            ? requested with { Status = status, IsEnabled = canRetry && (isRecovery || previous.IsEnabled), EffectName = null, Message = message }
            : requested with { Status = Idle(requested.Status), EffectName = null };

        var tone = requested.Tone;
        tone = tone.IsEnabled
            ? tone with
            {
                Status = status,
                IsEqualizerEnabled = tone.IsEqualizerEnabled && canRetry && (isRecovery || previous.Tone.IsEqualizerEnabled),
                IsBassEnabled = tone.IsBassEnabled && canRetry && (isRecovery || previous.Tone.IsBassEnabled),
                EffectName = null,
                Message = message,
            }
            : tone with { Status = Idle(tone.Status), EffectName = null };

        return result with { Tone = tone };
    }

    /// <summary>
    /// Publishes <paramref name="state"/> and, unless recovering, saves its switches as the user's choice;
    /// the health check runs while anything is switched on.
    /// </summary>
    private void Commit(AudioBoostState state, bool isRecovery)
    {
        if (state.IsAnyEnabled)
        {
            StartHealthCheck();
        }
        else
        {
            StopHealthCheck();
        }

        if (!isRecovery)
        {
            SaveSettings(settings => settings with
            {
                IsEnabled = state.IsEnabled,
                IsEqualizerEnabled = state.Tone.IsEqualizerEnabled,
                IsBassEnabled = state.Tone.IsBassEnabled,
            });
        }

        Publish(state);
    }

    private static IReadOnlyList<double> GainsFor(string presetId, EqualizerLayout layout, ApplicationSettings settings)
    {
        return presetId == EqualizerPresets.CustomId
            ? EqualizerCurve.Normalize(settings.CustomEqualizerGainsDecibels, layout)
            : EqualizerPresets.Find(presetId).GainsFor(layout);
    }

    /// <summary>
    /// Moves every band to <paramref name="target"/>, in steps when any band rises by more than
    /// <see cref="AudioBoostServiceOptions.ToneRampStepDecibels"/>.
    /// </summary>
    private async Task RampLevelsAsync(double[] target, CancellationToken cancellationToken)
    {
        var from = _appliedLevels;
        if (from.SequenceEqual(target))
        {
            return;
        }

        var rise = target.Zip(from, (to, before) => to - before).Max();
        var count = Math.Max(1, (int)Math.Ceiling(rise / _options.ToneRampStepDecibels));
        for (var step = 1; step <= count; step++)
        {
            if (step > 1 && _options.RampStepDelay > TimeSpan.Zero)
            {
                await Task.Delay(_options.RampStepDelay, _timeProvider, cancellationToken);
            }

            var share = (double)step / count;
            ApplyLevels(step == count
                ? target
                : from.Zip(target, (before, to) => Math.Round(before + (to - before) * share, 2)).ToArray());
        }
    }

    private async Task RampGainAsync(double targetDecibels, CancellationToken cancellationToken)
    {
        var steps = GainRamp.Steps(_appliedGain, targetDecibels, _options.RampStepDecibels);
        for (var i = 0; i < steps.Count; i++)
        {
            if (i > 0 && _options.RampStepDelay > TimeSpan.Zero)
            {
                await Task.Delay(_options.RampStepDelay, _timeProvider, cancellationToken);
            }

            ApplyGain(steps[i]);
        }
    }

    private void ApplyGain(double decibels)
    {
        var effect = _effect ?? throw new InvalidOperationException("No sound effect is attached.");
        effect.SetGain(decibels);
        _appliedGain = decibels;
        UpdateEnabled(effect);
    }

    private void ApplyLevels(double[] levels)
    {
        var effect = _effect ?? throw new InvalidOperationException("No sound effect is attached.");
        effect.SetBandLevels(levels);
        _appliedLevels = levels;
        UpdateEnabled(effect);
    }

    /// <summary>
    /// The effect processes only while it changes the sound, so 100 % with everything flat is the untouched sound.
    /// </summary>
    private void UpdateEnabled(ISoundEffect effect)
    {
        var shouldEnable = _appliedGain > 0 || _appliedLevels.Any(level => level != 0);
        if (shouldEnable != _isEffectEnabled)
        {
            effect.SetEnabled(shouldEnable);
            _isEffectEnabled = shouldEnable;
        }
    }

    private void ReleaseEffect()
    {
        var effect = _effect;
        if (effect is null)
        {
            return;
        }

        _effect = null;
        _appliedGain = 0;
        _appliedLevels = [];
        _isEffectEnabled = false;

        try
        {
            effect.SetEnabled(false);
        }
        catch (Exception exception)
        {
            _logger.LogDebug(exception, $"Disabling {effect.Name} before release failed.");
        }

        try
        {
            effect.Dispose();
            _logger.LogInformation($"Released {effect.Name}; the original sound processing is restored.");
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, $"Releasing {effect.Name} failed.");
        }
    }

    private void StartHealthCheck()
    {
        if (_options.HealthCheckInterval is not { } interval || _healthCheck is not null)
        {
            return;
        }

        _healthCheck = CancellationTokenSource.CreateLinkedTokenSource(_lifetime.Token);
        _ = RunHealthCheckAsync(interval, _healthCheck.Token);
    }

    private void StopHealthCheck()
    {
        var healthCheck = _healthCheck;
        if (healthCheck is null)
        {
            return;
        }

        _healthCheck = null;
        healthCheck.Cancel();
        healthCheck.Dispose();
    }

    private async Task RunHealthCheckAsync(TimeSpan interval, CancellationToken cancellationToken)
    {
        try
        {
            using var timer = new PeriodicTimer(interval, _timeProvider);
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await VerifyAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // Everything switched off, or disposed.
        }
        catch (ObjectDisposedException)
        {
            // Disposed between ticks.
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "The effect health check stopped.");
        }
    }

    private void SaveSettings(Func<ApplicationSettings, ApplicationSettings> update)
    {
        try
        {
            _settingsStore.Update(update);
        }
        catch (Exception exception)
        {
            // Losing a preference must not take the sound down with it.
            _logger.LogError(exception, "Saving settings failed.");
        }
    }

    private static string Describe(IReadOnlyList<double> levels, IReadOnlyList<double> frequencies)
    {
        // Every eighth band is enough to recognise the curve in the log.
        return string.Join(", ", levels
            .Select((level, band) => (level, band))
            .Where(pair => pair.band % 8 == 0)
            .Select(pair => $"{frequencies[pair.band]:0} Hz {pair.level:+0.0;-0.0;0}"));
    }

    private void Publish(AudioBoostState state)
    {
        Volatile.Write(ref _state, state);
        var tone = state.Tone;
        _logger.LogInformation($"State: {state.Status}, enabled: {state.IsEnabled}, {state.GainPercent} % ({state.GainDecibels:+0.00;-0.00} dB), effect: {state.EffectName ?? "none"}{(state.Message is null ? string.Empty : $", message: {state.Message}")}; " +
            $"tone: {tone.Status}, equalizer: {(tone.IsEqualizerEnabled ? "on" : "off")} {tone.EqualizerPresetId} [{string.Join(" ", tone.EqualizerGainsDecibels)}], bass: {(tone.IsBassEnabled ? "on" : "off")} {tone.BassDecibels:+0.#;-0.#;0} dB{(tone.Message is null ? string.Empty : $", message: {tone.Message}")}.");
        StateChanged?.Invoke(this, state);
    }
}
