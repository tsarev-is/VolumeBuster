namespace VolumeBuster.Audio;

/// <summary>
/// A named equalizer curve.
/// </summary>
/// <param name="Id">
/// Stable identifier kept in the settings.
/// </param>
/// <param name="GainsDecibels">
/// Gain of every band of <see cref="EqualizerLayout.Default"/>.
/// </param>
public sealed record EqualizerPreset(string Id, IReadOnlyList<double> GainsDecibels)
{
    /// <summary>
    /// Name shown to the user, in the current interface language.
    /// </summary>
    public string Name => EqualizerPresets.NameOf(Id);

    /// <summary>
    /// The same curve on <paramref name="layout"/>.
    /// </summary>
    public IReadOnlyList<double> GainsFor(EqualizerLayout layout)
    {
        return layout == EqualizerLayout.Default
            ? GainsDecibels
            : EqualizerCurve.Resample(GainsDecibels, EqualizerLayout.Default, layout);
    }
}
