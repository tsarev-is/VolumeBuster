using VolumeX.Threading;

namespace VolumeX.Tests.Fakes;

/// <summary>
/// Runs "UI thread" work right away on the calling thread.
/// </summary>
internal sealed class ImmediateUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        action();
    }
}
