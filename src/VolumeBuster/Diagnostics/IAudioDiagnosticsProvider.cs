namespace VolumeBuster.Diagnostics;

/// <summary>
/// Reports the audio route, volume and effect support of the device.
/// </summary>
public interface IAudioDiagnosticsProvider
{
    /// <summary>
    /// Latest snapshot.
    /// </summary>
    AudioDiagnostics Current { get; }

    /// <summary>
    /// Raised when the route or volume changes, possibly on a background thread.
    /// </summary>
    event EventHandler? Changed;

    /// <summary>
    /// Re-reads the platform state and raises <see cref="Changed"/>.
    /// </summary>
    void Refresh();
}
