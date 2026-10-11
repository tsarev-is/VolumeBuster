using CommunityToolkit.Mvvm.ComponentModel;
using VolumeBuster.Platform;

namespace VolumeBuster.ViewModels;

/// <summary>
/// The only screen: a switch and a slider for the extra gain over the system maximum.
/// </summary>
public sealed partial class MainViewModel : ObservableObject
{
    /// <summary>
    /// Gain at the right end of the slider, in millibels (+15 dB).
    /// </summary>
    public const int MaxGainMillibels = 1500;

    private readonly IVolumeBooster _booster;

    public MainViewModel(IVolumeBooster booster)
    {
        _booster = booster;
    }

    public bool IsSupported => _booster.IsSupported;

    [ObservableProperty]
    public partial bool IsEnabled { get; set; }

    /// <summary>
    /// Slider position, 0–100 % of <see cref="MaxGainMillibels"/>.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(GainText))]
    public partial double Level { get; set; } = 50;

    public string GainText => $"+{GainMillibels / 100.0:0.#} дБ";

    private int GainMillibels => (int)Math.Round(Level / 100 * MaxGainMillibels);

    partial void OnIsEnabledChanged(bool value) => Apply();

    partial void OnLevelChanged(double value) => Apply();

    private void Apply() => _booster.SetGain(IsEnabled ? GainMillibels : 0);
}
