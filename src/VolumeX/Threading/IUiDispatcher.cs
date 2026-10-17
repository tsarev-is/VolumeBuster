namespace VolumeX.Threading;

/// <summary>
/// Runs code on the UI thread.
/// </summary>
public interface IUiDispatcher
{
    /// <summary>
    /// Queues <paramref name="action"/> to the UI thread.
    /// </summary>
    void Post(Action action);
}
