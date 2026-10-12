using VolumeBuster.Diagnostics;

namespace VolumeBuster.Tests.Fakes;

/// <summary>
/// Diagnostics the test sets directly.
/// </summary>
internal sealed class FakeDiagnosticsProvider : IAudioDiagnosticsProvider
{
    public AudioDiagnostics Current { get; private set; } =
        new("Android 16 (API 36)", "Nothing A059", "Bluetooth A2DP · Nothing Ear (a)", "Nothing Ear (a)", 16, 16, "Dynamics Processing, Loudness Enhancer");

    public event EventHandler? Changed;

    public void Refresh()
    {
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Set(AudioDiagnostics diagnostics)
    {
        Current = diagnostics;
        Refresh();
    }
}
