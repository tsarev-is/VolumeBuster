using Microsoft.Extensions.Logging;

namespace VolumeX.Android.Logging;

/// <summary>
/// Sends <see cref="ILogger"/> output to logcat under the <see cref="LogcatLogger.Tag"/> tag.
/// </summary>
internal sealed class LogcatLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new LogcatLogger(categoryName);
    }

    public void Dispose()
    {
    }
}
