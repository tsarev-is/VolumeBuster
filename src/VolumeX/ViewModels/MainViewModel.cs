using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using VolumeX.Audio;
using VolumeX.Diagnostics;
using VolumeX.Localization;
using VolumeX.Settings;
using VolumeX.Threading;

namespace VolumeX.ViewModels;

/// <summary>
/// The only screen: the volume boost and the bass level as dials, the equalizer with 5, 7 or 10 bands, each with
/// its own switch, and diagnostics.
/// </summary>
public sealed partial class MainViewModel : ObservableObject, IDisposable
{
    private readonly IAudioBoostService _boost;
    private readonly ISettingsStore _settings;
    private readonly IAudioDiagnosticsProvider _diagnostics;
    private readonly IUiDispatcher _dispatcher;
    private readonly ILogger<MainViewModel> _logger;
    private readonly EqualizerPreset _customPreset = new(EqualizerPresets.CustomId, []);

    private AudioBoostState _state;
    private bool _isSyncing;
    private IReadOnlyList<double>? _pendingGains;
    private int _isApplyingGains;

    public MainViewModel(
        IAudioBoostService boost,
        ISettingsStore settings,
        IAudioDiagnosticsProvider diagnostics,
        IUiDispatcher dispatcher,
        ILogger<MainViewModel> logger)
    {
        _boost = boost;
        _settings = settings;
        _diagnostics = diagnostics;
        _dispatcher = dispatcher;
        _logger = logger;

        PresetOptions = [.. EqualizerPresets.All, _customPreset];

        _state = boost.State;
        _isSyncing = true;
        GainPercent = _state.GainPercent;
        IsBoostEnabled = _state.IsEnabled;
        BassDecibels = _state.Tone.BassDecibels;
        IsBassEnabled = _state.Tone.IsBassEnabled;
        EqualizerBandCount = _state.Tone.EqualizerLayout.BandCount;
        EqualizerLabels = _state.Tone.EqualizerLayout.Labels;
        EqualizerGains = _state.Tone.EqualizerGainsDecibels;
        SelectedPreset = PresetFor(_state.Tone.EqualizerPresetId);
        IsEqualizerEnabled = _state.Tone.IsEqualizerEnabled;
        _isSyncing = false;
        Diagnostics = diagnostics.Current;

        boost.StateChanged += OnBoostStateChanged;
        diagnostics.Changed += OnDiagnosticsChanged;
    }

    public double MinPercent => GainCalculator.MinPercent;

    public double MaxPercent => GainCalculator.MaxPercent;

    public double StepPercent => GainCalculator.StepPercent;

    public double MinBassDecibels => ToneCurve.MinBassDecibels;

    public double MaxBassDecibels => ToneCurve.MaxBassDecibels;

    public double BassStepDecibels => ToneCurve.BassStepDecibels;

    public double MinBandGainDecibels => EqualizerCurve.MinGainDecibels;

    public double MaxBandGainDecibels => EqualizerCurve.MaxGainDecibels;

    public double BandGainStepDecibels => EqualizerCurve.GainStepDecibels;

    /// <summary>
    /// The presets in the drop-down, the user's own curve last.
    /// </summary>
    public IReadOnlyList<EqualizerPreset> PresetOptions { get; }

    /// <summary>
    /// The volume boost switch. Reflects the engine: it flips back off when the device refuses the boost.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBoostEnabled { get; set; }

    /// <summary>
    /// The volume boost slider, always within the range and on a step.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GainPercentText), nameof(GainDecibelsText), nameof(GainDetailsText))]
    public partial double GainPercent { get; set; }

    /// <summary>
    /// The bass switch, independent of the volume boost and the equalizer.
    /// </summary>
    [ObservableProperty]
    public partial bool IsBassEnabled { get; set; }

    /// <summary>
    /// The bass level slider in decibels, negative to cut the bass; always within the range and on a step.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BassText))]
    public partial double BassDecibels { get; set; }

    /// <summary>
    /// How many band sliders the equalizer shows: 5, 7 or 10.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsFiveBands), nameof(IsSevenBands), nameof(IsTenBands))]
    public partial int EqualizerBandCount { get; set; }

    /// <summary>
    /// Frequency labels under the band sliders.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> EqualizerLabels { get; private set; }

    /// <summary>
    /// The equalizer switch, independent of the volume boost and the bass level.
    /// </summary>
    [ObservableProperty]
    public partial bool IsEqualizerEnabled { get; set; }

    /// <summary>
    /// The equalizer curve shown and edited in the band sliders: a gain for every reference frequency.
    /// Moving a band switches the preset to the user's own curve.
    /// </summary>
    [ObservableProperty]
    public partial IReadOnlyList<double> EqualizerGains { get; set; }

    /// <summary>
    /// The preset in the drop-down.
    /// </summary>
    [ObservableProperty]
    public partial EqualizerPreset SelectedPreset { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OutputRouteText), nameof(OutputDeviceText), nameof(MediaVolumeText),
        nameof(SystemVersionText), nameof(DeviceModelText), nameof(AvailableEffectsText))]
    public partial AudioDiagnostics Diagnostics { get; private set; }

    /// <summary>
    /// The one-time warning about loud playback.
    /// </summary>
    [ObservableProperty]
    public partial bool IsWarningVisible { get; private set; }

    /// <summary>
    /// What the volume boost actually does.
    /// </summary>
    public AudioBoostStatus Status => _state.Status;

    /// <summary>
    /// What the equalizer and the bass level actually do.
    /// </summary>
    public AudioBoostStatus ToneStatus => _state.Tone.Status;

    /// <summary>
    /// The segments of the band count switch; setting one selects it, the others follow.
    /// </summary>
    public bool IsFiveBands
    {
        get => EqualizerBandCount == 5;
        set => SelectBandCount(value, 5);
    }

    public bool IsSevenBands
    {
        get => EqualizerBandCount == 7;
        set => SelectBandCount(value, 7);
    }

    public bool IsTenBands
    {
        get => EqualizerBandCount == 10;
        set => SelectBandCount(value, 10);
    }

    public string GainPercentText => string.Format(CultureInfo.CurrentCulture, Strings.PercentFormat, GainPercent);

    public string GainDecibelsText => string.Format(CultureInfo.CurrentCulture, Strings.GainDecibelsFormat,
        GainCalculator.PercentToDecibels(GainPercent));

    /// <summary>
    /// The bass level without its unit, for the middle of the dial: "+6", "−4.5", "0".
    /// </summary>
    public string BassText => BassDecibels.ToString("+0.#;−0.#;0", CultureInfo.CurrentCulture);

    /// <summary>
    /// Why the volume boost does not work; null while it is fine.
    /// </summary>
    public string? BoostProblemText => ProblemText(_state.Status, _state.IsEnabled, _state.Message);

    public bool IsBoostProblemVisible => BoostProblemText is not null;

    /// <summary>
    /// Why the equalizer or the bass level does not work; null while they are fine.
    /// </summary>
    public string? ToneProblemText => ProblemText(_state.Tone.Status, _state.Tone.IsEnabled, _state.Tone.Message);

    public bool IsToneProblemVisible => ToneProblemText is not null;

    public string OutputRouteText => Diagnostics.OutputRoute;

    /// <summary>
    /// Where the sound goes, in a few words for the header.
    /// </summary>
    public string OutputDeviceText => Diagnostics.OutputDevice;

    public string MediaVolumeText => Diagnostics.MediaVolumeMax > 0
        ? string.Format(CultureInfo.CurrentCulture, Strings.MediaVolumeFormat, Diagnostics.MediaVolume, Diagnostics.MediaVolumeMax)
        : "—";

    public string SystemVersionText => Diagnostics.SystemVersion;

    public string DeviceModelText => Diagnostics.DeviceModel;

    public string AvailableEffectsText => Diagnostics.AvailableEffects;

    public string EffectText => (_state.EffectName ?? _state.Tone.EffectName) is { } name
        ? string.Format(CultureInfo.CurrentCulture, Strings.EffectFormat, name)
        : "—";

    public string GainDetailsText => string.Format(CultureInfo.CurrentCulture, Strings.GainDetailsFormat,
        GainCalculator.PercentToDecibels(GainPercent), GainCalculator.PercentToAmplitude(GainPercent));

    [RelayCommand]
    private void AcknowledgeWarning()
    {
        IsWarningVisible = false;
        _settings.Update(settings => settings with { IsWarningAcknowledged = true });
    }

    public void Dispose()
    {
        _boost.StateChanged -= OnBoostStateChanged;
        _diagnostics.Changed -= OnDiagnosticsChanged;
    }

    partial void OnIsBoostEnabledChanged(bool value)
    {
        if (_isSyncing)
        {
            return;
        }

        ShowWarningOnFirstUse(value);
        _ = RunAsync(value ? _boost.EnableAsync : _boost.DisableAsync);
    }

    partial void OnGainPercentChanged(double value)
    {
        var normalized = GainCalculator.Normalize(value);
        if (!normalized.Equals(value))
        {
            GainPercent = normalized;
            return;
        }

        if (_isSyncing)
        {
            return;
        }

        _ = RunAsync(cancellationToken => _boost.SetGainAsync(normalized, cancellationToken));
    }

    partial void OnIsBassEnabledChanged(bool value)
    {
        if (_isSyncing)
        {
            return;
        }

        ShowWarningOnFirstUse(value);
        _ = RunAsync(cancellationToken => _boost.SetBassEnabledAsync(value, cancellationToken));
    }

    partial void OnBassDecibelsChanged(double value)
    {
        var normalized = ToneCurve.NormalizeBass(value);
        if (!normalized.Equals(value))
        {
            BassDecibels = normalized;
            return;
        }

        if (_isSyncing)
        {
            return;
        }

        _ = RunAsync(cancellationToken => _boost.SetBassAsync(normalized, cancellationToken));
    }

    partial void OnIsEqualizerEnabledChanged(bool value)
    {
        if (_isSyncing)
        {
            return;
        }

        ShowWarningOnFirstUse(value);
        _ = RunAsync(cancellationToken => _boost.SetEqualizerEnabledAsync(value, cancellationToken));
    }

    partial void OnEqualizerBandCountChanged(int value)
    {
        if (_isSyncing)
        {
            return;
        }

        // The new bands appear at once with the same curve; the engine confirms it a moment later.
        var current = EqualizerLayout.For(EqualizerGains.Count);
        var layout = EqualizerLayout.For(value);
        Sync(() =>
        {
            EqualizerLabels = layout.Labels;
            EqualizerGains = SelectedPreset.Id == EqualizerPresets.CustomId
                ? EqualizerCurve.Resample(EqualizerGains, current, layout)
                : SelectedPreset.GainsFor(layout);
        });

        _ = RunAsync(cancellationToken => _boost.SetEqualizerBandCountAsync(value, cancellationToken));
    }

    partial void OnSelectedPresetChanged(EqualizerPreset value)
    {
        // A list control clears its selection while it is being rebuilt; that is not a choice.
        if (_isSyncing || value is null)
        {
            return;
        }

        _ = RunAsync(cancellationToken => _boost.SetEqualizerPresetAsync(value.Id, cancellationToken));
    }

    partial void OnEqualizerGainsChanged(IReadOnlyList<double> value)
    {
        if (_isSyncing || value is null)
        {
            return;
        }

        Sync(() => SelectedPreset = _customPreset);

        // Dragging produces a stream of curves; the engine gets the latest one whenever it is free.
        Volatile.Write(ref _pendingGains, value);
        if (Interlocked.Exchange(ref _isApplyingGains, 1) == 0)
        {
            _ = ApplyPendingGainsAsync();
        }
    }

    private async Task ApplyPendingGainsAsync()
    {
        while (true)
        {
            while (Interlocked.Exchange(ref _pendingGains, null) is { } gains)
            {
                await RunAsync(cancellationToken => _boost.SetEqualizerGainsAsync(gains, cancellationToken));
            }

            Volatile.Write(ref _isApplyingGains, 0);

            // A curve may have arrived between the last check and the reset.
            if (Volatile.Read(ref _pendingGains) is null || Interlocked.Exchange(ref _isApplyingGains, 1) != 0)
            {
                return;
            }
        }
    }

    private void SelectBandCount(bool isSelected, int bandCount)
    {
        if (isSelected)
        {
            EqualizerBandCount = bandCount;
        }
    }

    private void ShowWarningOnFirstUse(bool isSwitchedOn)
    {
        if (isSwitchedOn && !_settings.Load().IsWarningAcknowledged)
        {
            IsWarningVisible = true;
        }
    }

    private async Task RunAsync(Func<CancellationToken, Task> operation)
    {
        try
        {
            await operation(CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Audio operation failed.");
            _dispatcher.Post(() => ApplyState(_boost.State with { Status = AudioBoostStatus.Error, Message = exception.Message }));
        }
    }

    private void OnBoostStateChanged(object? sender, AudioBoostState state)
    {
        _dispatcher.Post(() => ApplyState(state));
    }

    private void OnDiagnosticsChanged(object? sender, EventArgs e)
    {
        _dispatcher.Post(() => Diagnostics = _diagnostics.Current);
    }

    private void ApplyState(AudioBoostState state)
    {
        var previous = _state;
        _state = state;

        // The sliders and the curve are owned by the controls: echoing older engine states back would make them
        // jump while dragging. The switches follow the engine, which flips them off when a device refuses, and
        // a newly selected preset brings its curve.
        Sync(() =>
        {
            IsBoostEnabled = state.IsEnabled;
            IsBassEnabled = state.Tone.IsBassEnabled;
            IsEqualizerEnabled = state.Tone.IsEqualizerEnabled;
            if (state.Tone.EqualizerPresetId != previous.Tone.EqualizerPresetId
                || state.Tone.EqualizerPresetId != EqualizerPresets.CustomId
                || state.Tone.EqualizerLayout != previous.Tone.EqualizerLayout)
            {
                EqualizerBandCount = state.Tone.EqualizerLayout.BandCount;
                EqualizerLabels = state.Tone.EqualizerLayout.Labels;
                SelectedPreset = PresetFor(state.Tone.EqualizerPresetId);
                EqualizerGains = state.Tone.EqualizerGainsDecibels;
            }
        });

        OnPropertyChanged(nameof(Status));
        OnPropertyChanged(nameof(ToneStatus));
        OnPropertyChanged(nameof(BoostProblemText));
        OnPropertyChanged(nameof(IsBoostProblemVisible));
        OnPropertyChanged(nameof(ToneProblemText));
        OnPropertyChanged(nameof(IsToneProblemVisible));
        OnPropertyChanged(nameof(EffectText));
    }

    private void Sync(Action update)
    {
        var wasSyncing = _isSyncing;
        _isSyncing = true;
        try
        {
            update();
        }
        finally
        {
            _isSyncing = wasSyncing;
        }
    }

    private EqualizerPreset PresetFor(string presetId)
    {
        return presetId == EqualizerPresets.CustomId ? _customPreset : EqualizerPresets.Find(presetId);
    }

    private static string? ProblemText(AudioBoostStatus status, bool isEnabled, string? message)
    {
        return status switch
        {
            AudioBoostStatus.Unsupported => message ?? Strings.ProblemUnsupported,
            AudioBoostStatus.Error when isEnabled => string.Format(CultureInfo.CurrentCulture, Strings.ProblemRetryingFormat, message),
            AudioBoostStatus.Error => message ?? Strings.ProblemUnknown,
            _ => null,
        };
    }
}
