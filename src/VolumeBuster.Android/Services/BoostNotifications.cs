using System.Globalization;
using Android.App;
using Android.Content;
using Android.Graphics.Drawables;
using VolumeBuster.Audio;

namespace VolumeBuster.Android.Services;

/// <summary>
/// The notification Android requires for the foreground service, with a button that turns everything off.
/// </summary>
/// <remarks>
/// The app does not hold POST_NOTIFICATIONS, so on Android 13+ the notification never reaches the shade: the
/// service shows up only under "Active apps" in quick settings. On Android 12 it sits silently, collapsed at the
/// bottom of the shade, without a status bar icon.
/// </remarks>
internal static class BoostNotifications
{
    public const int NotificationId = 1;

    private const string ChannelId = "service";

    /// <summary>
    /// The earlier, low-importance channel; removed so it does not linger in the app's notification settings.
    /// </summary>
    private const string ObsoleteChannelId = "boost";

    private const int OpenAppRequestCode = 0;
    private const int TurnOffRequestCode = 1;

    public static void EnsureChannel(Context context)
    {
        var manager = NotificationManager.FromContext(context);
        if (manager is null)
        {
            return;
        }

        // Re-creating an existing channel only renames it, which keeps it in the current language.
        var channel = new NotificationChannel(ChannelId, context.GetString(Resource.String.notification_channel_name), NotificationImportance.Min)
        {
            Description = context.GetString(Resource.String.notification_channel_description),
        };
        channel.SetShowBadge(false);
        channel.SetSound(null, null);
        manager.CreateNotificationChannel(channel);

        if (manager.GetNotificationChannel(ObsoleteChannelId) is not null)
        {
            try
            {
                manager.DeleteNotificationChannel(ObsoleteChannelId);
            }
            catch (Java.Lang.SecurityException)
            {
                // Still attached to the running service; it goes on the next start.
            }
        }
    }

    public static Notification Build(Context context, AudioBoostState state)
    {
        EnsureChannel(context);

        var openApp = PendingIntent.GetActivity(
            context,
            OpenAppRequestCode,
            new Intent(context, typeof(MainActivity)).SetFlags(ActivityFlags.SingleTop),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var turnOff = PendingIntent.GetService(
            context,
            TurnOffRequestCode,
            new Intent(context, typeof(BoostForegroundService)).SetAction(BoostForegroundService.ActionStop),
            PendingIntentFlags.Immutable | PendingIntentFlags.UpdateCurrent);

        var (title, text) = Describe(context, state);

        var icon = Icon.CreateWithResource(context, Resource.Drawable.ic_notification);
        var builder = new Notification.Builder(context, ChannelId)
            .SetSmallIcon(Resource.Drawable.ic_notification)!
            .SetContentTitle(title)!
            .SetContentText(text)!
            .SetContentIntent(openApp)!
            .SetOngoing(true)!
            .SetOnlyAlertOnce(true)!
            .SetShowWhen(false)!
            .SetCategory(Notification.CategoryService)!
            .AddAction(new Notification.Action.Builder(icon, context.GetString(Resource.String.notification_turn_off), turnOff).Build())!
            .SetForegroundServiceBehavior((int)NotificationForegroundService.Immediate)!;

        return builder.Build()!;
    }

    private static (string Title, string Text) Describe(Context context, AudioBoostState state)
    {
        if (state.Status == AudioBoostStatus.Error)
        {
            return (context.GetString(Resource.String.notification_boost_error),
                state.Message ?? context.GetString(Resource.String.notification_recovering));
        }

        if (state.Tone.Status == AudioBoostStatus.Error)
        {
            return (context.GetString(Resource.String.notification_tone_error),
                state.Tone.Message ?? context.GetString(Resource.String.notification_recovering));
        }

        var culture = CultureInfo.CurrentCulture;
        var parts = new List<string>();
        if (state.Status == AudioBoostStatus.Active)
        {
            parts.Add(context.GetString(Resource.String.notification_boost,
                new Java.Lang.String(state.GainPercent.ToString("0", culture)),
                new Java.Lang.String(state.GainDecibels.ToString("0.0", culture))));
        }

        if (state.Tone.Status == AudioBoostStatus.Active && state.Tone.IsBassEnabled)
        {
            parts.Add(context.GetString(Resource.String.notification_bass,
                new Java.Lang.String(state.Tone.BassDecibels.ToString("+0.#;−0.#;0", culture))));
        }

        if (state.Tone.Status == AudioBoostStatus.Active && state.Tone.IsEqualizerEnabled)
        {
            parts.Add(context.GetString(Resource.String.notification_equalizer, new Java.Lang.String(state.Tone.EqualizerPresetName)));
        }

        return parts.Count == 0
            ? ("VolumeX", context.GetString(Resource.String.notification_connecting))
            : (string.Join(" · ", parts), context.GetString(Resource.String.notification_processing));
    }

    public static void Update(Context context, AudioBoostState state)
    {
        NotificationManager.FromContext(context)?.Notify(NotificationId, Build(context, state));
    }
}
