using VolumeX.Localization;

namespace VolumeX.Audio;

/// <summary>
/// The equalizer presets offered to the user, defined on the ten-band layout and resampled for the others.
/// </summary>
/// <remarks>
/// The music presets are the stock Android ones (AOSP LVM bundle, <c>kSoftPresets</c>, defined at 60, 230, 910,
/// 3600 and 14000 Hz) read off the same monotone curve at the octave bands, so they sound as in any system
/// equalizer. "Acoustic" adds the usual warm low end and airy top; "Voice" is for speech: less boom below
/// 250 Hz, more presence at 1–4 kHz.
/// </remarks>
public static class EqualizerPresets
{
    /// <summary>
    /// Identifier of the preset a fresh install starts with: the flat curve.
    /// </summary>
    public const string DefaultId = "flat";

    /// <summary>
    /// Identifier of the user's own curve, set by moving the band sliders; it is not one of <see cref="All"/>.
    /// </summary>
    public const string CustomId = "custom";

    /// <summary>
    /// Name shown for <see cref="CustomId"/>, in the current interface language.
    /// </summary>
    public static string CustomName => Strings.EqualizerPresetCustom;

    public static IReadOnlyList<EqualizerPreset> All { get; } =
    [
        new(DefaultId, [0, 0, 0, 0, 0, 0, 0, 0, 0, 0]),
        new("acoustic", [4.5, 4.5, 3.5, 1, 1.5, 1.5, 3, 3.5, 3, 1.5]),
        new("classical", [5, 5, 4, 3, -0.5, -2, 1.5, 4, 4, 4]),
        new("jazz", [4, 4, 3, 2, -0.5, -2, 0, 2.5, 4, 5]),
        new("pop", [-1, -1, 0.5, 2, 4, 5, 3, 0.5, -1, -2]),
        new("rock", [5, 5, 4, 3, 0.5, -1, 1, 3, 4.5, 5]),
        new("hiphop", [5, 5, 4, 3, 1, 0, 0.5, 1, 2.5, 3]),
        new("dance", [6, 6, 2.5, 0, 1, 2, 3.5, 4, 2, 1]),
        new("vocal", [-2, -2, -1.5, -1, 1.5, 3, 4, 4, 2, 1]),
    ];

    /// <summary>
    /// The preset a fresh install starts with.
    /// </summary>
    public static EqualizerPreset Default => All[0];

    /// <summary>
    /// The preset with this identifier; unknown or missing identifiers give <see cref="Default"/>.
    /// </summary>
    public static EqualizerPreset Find(string? id)
    {
        return All.FirstOrDefault(preset => preset.Id == id) ?? Default;
    }

    /// <summary>
    /// <see cref="CustomId"/> or the identifier of a known preset; anything else gives <see cref="DefaultId"/>.
    /// </summary>
    public static string NormalizeId(string? id)
    {
        return id == CustomId ? CustomId : Find(id).Id;
    }

    /// <summary>
    /// Name shown for a preset identifier, the user's own curve included, in the current interface language;
    /// unknown or missing identifiers give the name of <see cref="Default"/>.
    /// </summary>
    public static string NameOf(string? id)
    {
        return NormalizeId(id) switch
        {
            CustomId => CustomName,
            "acoustic" => Strings.EqualizerPresetAcoustic,
            "classical" => Strings.EqualizerPresetClassical,
            "jazz" => Strings.EqualizerPresetJazz,
            "pop" => Strings.EqualizerPresetPop,
            "rock" => Strings.EqualizerPresetRock,
            "hiphop" => Strings.EqualizerPresetHipHop,
            "dance" => Strings.EqualizerPresetDance,
            "vocal" => Strings.EqualizerPresetVocal,
            _ => Strings.EqualizerPresetFlat,
        };
    }
}
