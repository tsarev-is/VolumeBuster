namespace VolumeX.Diagnostics;

/// <summary>
/// Device facts shown in the diagnostics section.
/// </summary>
/// <param name="SystemVersion">
/// OS name and version, e.g. "Android 16 (API 36)".
/// </param>
/// <param name="DeviceModel">
/// Manufacturer and model.
/// </param>
/// <param name="OutputRoute">
/// Where media is playing right now, e.g. "Bluetooth A2DP · WH-1000XM4".
/// </param>
/// <param name="OutputDevice">
/// The same in a few words for the header: the device name, or its kind when it has none, e.g. "WH-1000XM4".
/// </param>
/// <param name="MediaVolume">
/// Current media volume step.
/// </param>
/// <param name="MediaVolumeMax">
/// Highest media volume step.
/// </param>
/// <param name="AvailableEffects">
/// Gain-capable effects the platform reports.
/// </param>
public sealed record AudioDiagnostics(
    string SystemVersion,
    string DeviceModel,
    string OutputRoute,
    string OutputDevice,
    int MediaVolume,
    int MediaVolumeMax,
    string AvailableEffects)
{
    /// <summary>
    /// Placeholder before the platform reports anything.
    /// </summary>
    public static AudioDiagnostics Unknown { get; } = new("—", "—", "—", "—", 0, 0, "—");

    /// <summary>
    /// The system media volume is below its maximum, so the boost is not the only way to get louder.
    /// </summary>
    public bool IsMediaVolumeBelowMax => MediaVolumeMax > 0 && MediaVolume < MediaVolumeMax;
}
