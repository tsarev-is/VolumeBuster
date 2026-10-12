using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Content.Res;
using Android.OS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VolumeBuster.Audio;

namespace VolumeBuster.Android.Services;

/// <summary>
/// Keeps the app process, and with it the audio effect, alive while other apps play.
/// </summary>
/// <remarks>
/// The effect belongs to the process: when Android kills a background process, AudioFlinger drops its effects
/// and the sound silently returns to normal. A foreground service is the supported way to stay alive.
/// It holds no wake lock and does no work of its own; type "specialUse" because none of the predefined
/// types fits an app that processes, but does not play, audio (declared in AndroidManifest.xml).
/// The service stops itself as soon as the boost, the equalizer and the bass level are all switched off.
/// Its notification is required by Android but stays hidden, see <see cref="BoostNotifications"/>.
/// </remarks>
[Service(Name = "app.volumebuster.BoostForegroundService", Exported = false)]
public sealed class BoostForegroundService : Service
{
    public const string ActionStart = "app.volumebuster.action.START";
    public const string ActionStop = "app.volumebuster.action.STOP";

    private IAudioBoostService? _boost;
    private ILogger<BoostForegroundService>? _logger;
    private Handler? _handler;

    /// <summary>
    /// True between creation and destruction of the service.
    /// </summary>
    public static bool IsRunning { get; private set; }

    public override IBinder? OnBind(Intent? intent)
    {
        return null;
    }

    public override void OnCreate()
    {
        base.OnCreate();
        IsRunning = true;

        var services = MainApplication.Services;
        _boost = services.GetRequiredService<IAudioBoostService>();
        _logger = services.GetRequiredService<ILogger<BoostForegroundService>>();
        _handler = new Handler(Looper.MainLooper!);
        _boost.StateChanged += OnBoostStateChanged;
    }

    public override StartCommandResult OnStartCommand(Intent? intent, StartCommandFlags flags, int startId)
    {
        var boost = _boost!;
        if (!TryStartForeground(boost.State))
        {
            StopSelf();
            return StartCommandResult.NotSticky;
        }

        switch (intent?.Action)
        {
            case ActionStop:
                _logger!.LogInformation("Turn off requested from the notification.");
                _ = boost.DisableAllAsync();
                break;
            case null:
                // Restarted by the system after the process was killed while the boost was on.
                _logger!.LogInformation("Service restarted by the system; restoring the boost.");
                _ = RestoreAfterRestartAsync(boost);
                break;
            default:
                if (!boost.State.IsAnyEnabled)
                {
                    StopNow();
                }

                break;
        }

        return StartCommandResult.Sticky;
    }

    public override void OnConfigurationChanged(Configuration? newConfig)
    {
        base.OnConfigurationChanged(newConfig);

        // The language may have changed; the application has already switched the culture by now.
        var state = _boost!.State;
        if (state.IsAnyEnabled)
        {
            BoostNotifications.Update(this, state);
        }
    }

    public override void OnDestroy()
    {
        IsRunning = false;
        if (_boost is not null)
        {
            _boost.StateChanged -= OnBoostStateChanged;
        }

        base.OnDestroy();
    }

    private bool TryStartForeground(AudioBoostState state)
    {
        try
        {
            var notification = BoostNotifications.Build(this, state);
            if (OperatingSystem.IsAndroidVersionAtLeast(34))
            {
                StartForeground(BoostNotifications.NotificationId, notification, ForegroundService.TypeSpecialUse);
            }
            else
            {
                StartForeground(BoostNotifications.NotificationId, notification);
            }

            return true;
        }
        catch (Exception exception)
        {
            // ForegroundServiceStartNotAllowedException: Android refused a start from the background.
            _logger!.LogWarning(exception, "Could not enter the foreground; the boost lasts only while the app process lives.");
            return false;
        }
    }

    private async Task RestoreAfterRestartAsync(IAudioBoostService boost)
    {
        try
        {
            await boost.RestoreAsync();
        }
        catch (Exception exception)
        {
            _logger!.LogError(exception, "Restoring the boost after a restart failed.");
        }

        if (!boost.State.IsAnyEnabled)
        {
            _handler!.Post(StopNow);
        }
    }

    private void OnBoostStateChanged(object? sender, AudioBoostState state)
    {
        _handler!.Post(() =>
        {
            if (state.IsAnyEnabled)
            {
                BoostNotifications.Update(this, state);
            }
            else
            {
                StopNow();
            }
        });
    }

    private void StopNow()
    {
        StopForeground(StopForegroundFlags.Remove);
        StopSelf();
    }
}
