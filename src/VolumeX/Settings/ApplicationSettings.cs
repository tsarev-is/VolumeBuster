using VolumeX.Audio;

namespace VolumeX.Settings;

/// <summary>
/// User preferences kept on the device.
/// </summary>
/// <param name="GainPercent">
/// Selected volume percentage.
/// </param>
/// <param name="IsEnabled">
/// The user left the volume boost switched on and the device accepted it.
/// </param>
/// <param name="IsWarningAcknowledged">
/// The user has seen the warning about loud playback.
/// </param>
/// <param name="IsEqualizerEnabled">
/// The user left the equalizer switched on and the device accepted it.
/// </param>
/// <param name="EqualizerPresetId">
/// Selected equalizer preset, see <see cref="EqualizerPresets"/>.
/// </param>
/// <param name="IsBassEnabled">
/// The user left the bass level switched on and the device accepted it.
/// </param>
/// <param name="BassDecibels">
/// Selected bass level at 60 Hz, in decibels.
/// </param>
/// <param name="CustomEqualizerGainsDecibels">
/// The user's own equalizer curve, kept while a preset is selected; null until the user makes one.
/// </param>
/// <param name="EqualizerBandCount">
/// How many band sliders the equalizer has: 5, 7 or 10, see <see cref="EqualizerLayout"/>.
/// </param>
public sealed record ApplicationSettings(
    double GainPercent = GainCalculator.DefaultPercent,
    bool IsEnabled = false,
    bool IsWarningAcknowledged = false,
    bool IsEqualizerEnabled = false,
    string EqualizerPresetId = EqualizerPresets.DefaultId,
    bool IsBassEnabled = false,
    double BassDecibels = ToneCurve.DefaultBassDecibels,
    IReadOnlyList<double>? CustomEqualizerGainsDecibels = null,
    int EqualizerBandCount = 10);
