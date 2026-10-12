using Microsoft.Extensions.Logging;

namespace VolumeBuster.Android.Logging;

/// <summary>
/// One logger category written to logcat: <c>adb logcat -s VolumeBuster</c>.
/// </summary>
internal sealed class LogcatLogger : ILogger
{
    /// <summary>
    /// logcat tag of every message of the app.
    /// </summary>
    public const string Tag = "VolumeBuster";

    private readonly string _category;

    public LogcatLogger(string categoryName)
    {
        var lastDot = categoryName.LastIndexOf('.');
        _category = lastDot >= 0 ? categoryName[(lastDot + 1)..] : categoryName;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel != LogLevel.None;
    }

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = $"[{_category}] {formatter(state, exception)}";
        if (exception is not null)
        {
            message += $"{System.Environment.NewLine}{exception}";
        }

        global::Android.Util.Log.WriteLine(ToPriority(logLevel), Tag, message);
    }

    private static global::Android.Util.LogPriority ToPriority(LogLevel logLevel)
    {
        return logLevel switch
        {
            LogLevel.Trace => global::Android.Util.LogPriority.Verbose,
            LogLevel.Debug => global::Android.Util.LogPriority.Debug,
            LogLevel.Information => global::Android.Util.LogPriority.Info,
            LogLevel.Warning => global::Android.Util.LogPriority.Warn,
            LogLevel.Error => global::Android.Util.LogPriority.Error,
            _ => global::Android.Util.LogPriority.Assert,
        };
    }
}
