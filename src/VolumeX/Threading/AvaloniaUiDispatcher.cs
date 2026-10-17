using Avalonia.Threading;

namespace VolumeX.Threading;

/// <summary>
/// <see cref="IUiDispatcher"/> over Avalonia's UI thread.
/// </summary>
public sealed class AvaloniaUiDispatcher : IUiDispatcher
{
    public void Post(Action action)
    {
        Dispatcher.UIThread.Post(action);
    }
}
