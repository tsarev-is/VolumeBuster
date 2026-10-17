using Android.Content;
using Microsoft.Extensions.Logging;
using VolumeX.Audio;

namespace VolumeX.Android.Services;

/// <summary>
/// Starts <see cref="BoostForegroundService"/> when anything is switched on; the service stops itself.
/// </summary>
internal sealed class BoostServiceLauncher
{
    private readonly Context _context;
    private readonly ILogger<BoostServiceLauncher> _logger;

    public BoostServiceLauncher(Context context, IAudioBoostService boost, ILogger<BoostServiceLauncher> logger)
    {
        _context = context;
        _logger = logger;
        boost.StateChanged += OnBoostStateChanged;
    }

    private void OnBoostStateChanged(object? sender, AudioBoostState state)
    {
        if (!state.IsAnyEnabled || BoostForegroundService.IsRunning)
        {
            return;
        }

        try
        {
            var intent = new Intent(_context, typeof(BoostForegroundService)).SetAction(BoostForegroundService.ActionStart);
            _context.StartForegroundService(intent);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Starting the foreground service failed; the boost lasts only while the app process lives.");
        }
    }
}
