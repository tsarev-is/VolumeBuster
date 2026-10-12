using System.Globalization;

namespace VolumeBuster.Audio;

/// <summary>
/// How many band sliders the equalizer shows and where they sit: equally spaced on a logarithmic frequency axis
/// from the lowest band up to 16 kHz.
/// </summary>
public sealed class EqualizerLayout
{
    private const double TopFrequency = 16000;

    private EqualizerLayout(int bandCount, double octavesPerBand, IReadOnlyList<string> labels)
    {
        BandCount = bandCount;
        OctavesPerBand = octavesPerBand;
        Frequencies = Enumerable.Range(0, bandCount)
            .Select(band => TopFrequency * Math.Pow(2, -(bandCount - 1 - band) * octavesPerBand))
            .ToArray();
        Labels = labels;
    }

    /// <summary>
    /// Two octaves per band: 63 Hz to 16 kHz.
    /// </summary>
    public static EqualizerLayout Five { get; } = new(5, 2, ["63", "250", "1k", "4k", "16k"]);

    /// <summary>
    /// Four thirds of an octave per band: 63 Hz to 16 kHz.
    /// </summary>
    public static EqualizerLayout Seven { get; } = new(7, 4.0 / 3, ["63", "160", "400", "1k", "2.5k", "6.3k", "16k"]);

    /// <summary>
    /// The classic octave equalizer: 31 Hz to 16 kHz.
    /// </summary>
    public static EqualizerLayout Ten { get; } = new(10, 1, ["31", "63", "125", "250", "500", "1k", "2k", "4k", "8k", "16k"]);

    public static IReadOnlyList<EqualizerLayout> All { get; } = [Five, Seven, Ten];

    /// <summary>
    /// The layout a fresh install starts with; presets are defined on it.
    /// </summary>
    public static EqualizerLayout Default => Ten;

    public int BandCount { get; }

    /// <summary>
    /// Distance between neighbouring bands.
    /// </summary>
    public double OctavesPerBand { get; }

    /// <summary>
    /// Centre frequency of every band in hertz, lowest first.
    /// </summary>
    public IReadOnlyList<double> Frequencies { get; }

    /// <summary>
    /// Short label under every band ("63", "1k").
    /// </summary>
    public IReadOnlyList<string> Labels { get; }

    /// <summary>
    /// The layout with this many bands; anything else gives <see cref="Default"/>.
    /// </summary>
    public static EqualizerLayout For(int bandCount)
    {
        return All.FirstOrDefault(layout => layout.BandCount == bandCount) ?? Default;
    }

    /// <summary>
    /// Where <paramref name="frequency"/> lies in band units: 0 at the first band, 1 at the second, and so on;
    /// below 0 and above <see cref="BandCount"/> − 1 outside the bands.
    /// </summary>
    public double PositionOf(double frequency)
    {
        return Math.Log2(frequency / Frequencies[0]) / OctavesPerBand;
    }

    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"{BandCount} bands");
    }
}
