namespace VolumeBuster.Settings;

/// <summary>
/// Local storage for <see cref="ApplicationSettings"/>; nothing leaves the device.
/// </summary>
public interface ISettingsStore
{
    /// <summary>
    /// Saved settings, or defaults on a fresh install.
    /// </summary>
    ApplicationSettings Load();

    /// <summary>
    /// Atomically replaces the saved settings with <paramref name="change"/> applied to them.
    /// </summary>
    void Update(Func<ApplicationSettings, ApplicationSettings> change);
}
