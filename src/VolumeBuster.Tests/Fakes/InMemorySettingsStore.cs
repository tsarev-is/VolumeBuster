using VolumeBuster.Settings;

namespace VolumeBuster.Tests.Fakes;

/// <summary>
/// <see cref="ISettingsStore"/> kept in memory.
/// </summary>
internal sealed class InMemorySettingsStore : ISettingsStore
{
    public InMemorySettingsStore(ApplicationSettings? settings = null)
    {
        Settings = settings ?? new ApplicationSettings();
    }

    public ApplicationSettings Settings { get; private set; }

    public int SaveCount { get; private set; }

    public ApplicationSettings Load()
    {
        return Settings;
    }

    public void Update(Func<ApplicationSettings, ApplicationSettings> change)
    {
        Settings = change(Settings);
        SaveCount++;
    }
}
