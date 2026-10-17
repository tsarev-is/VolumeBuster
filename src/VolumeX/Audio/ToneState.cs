namespace VolumeX.Audio;

/// <summary>
/// Snapshot of the equalizer and the bass level.
/// </summary>
/// <param name="Status">
/// What the equalizer and the bass level actually do.
/// </param>
/// <param name="IsEqualizerEnabled">
/// The equalizer is switched on.
/// </param>
/// <param name="EqualizerLayout">
/// How many band sliders the equalizer has.
/// </param>
/// <param name="EqualizerPresetId">
/// Selected preset or <see cref="EqualizerPresets.CustomId"/>.
/// </param>
/// <param name="EqualizerGainsDecibels">
/// The equalizer curve in use: a gain for every band of <paramref name="EqualizerLayout"/>.
/// </param>
/// <param name="IsBassEnabled">
/// The bass level is switched on.
/// </param>
/// <param name="BassDecibels">
/// Selected bass level in decibels, from −9 to +9 in half steps; see <see cref="ToneCurve"/>.
/// </param>
/// <param name="EffectName">
/// The platform effect in use, if any.
/// </param>
/// <param name="Message">
/// Why the equalizer and the bass level are unsupported or failed; null otherwise.
/// </param>
public sealed record ToneState(
    AudioBoostStatus Status,
    bool IsEqualizerEnabled,
    EqualizerLayout EqualizerLayout,
    string EqualizerPresetId,
    IReadOnlyList<double> EqualizerGainsDecibels,
    bool IsBassEnabled,
    double BassDecibels,
    string? EffectName = null,
    string? Message = null)
{
    /// <summary>
    /// Everything off, with the default layout, preset and bass level.
    /// </summary>
    public static ToneState Off { get; } = new(
        AudioBoostStatus.Disabled,
        false,
        EqualizerLayout.Default,
        EqualizerPresets.Default.Id,
        EqualizerPresets.Default.GainsDecibels,
        false,
        ToneCurve.DefaultBassDecibels);

    /// <summary>
    /// The equalizer or the bass level is switched on.
    /// </summary>
    public bool IsEnabled => IsEqualizerEnabled || IsBassEnabled;

    /// <summary>
    /// Name of the selected preset, <see cref="EqualizerPresets.CustomName"/> for the user's own curve, in the
    /// current interface language.
    /// </summary>
    public string EqualizerPresetName => EqualizerPresets.NameOf(EqualizerPresetId);
}
