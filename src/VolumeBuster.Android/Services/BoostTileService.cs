using System.Globalization;
using Android.App;
using Android.Content;
using Android.Service.QuickSettings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VolumeBuster.Audio;

namespace VolumeBuster.Android.Services;

/// <summary>
/// Quick Settings tile: switches the boost on and off without opening the app.
/// </summary>
[Service(
    Name = "app.volumebuster.BoostTileService",
    Exported = true,
    Label = "@string/tile_label",
    Icon = "@drawable/ic_notification",
    Permission = "android.permission.BIND_QUICK_SETTINGS_TILE")]
[IntentFilter([ActionQsTile])]
[MetaData("android.service.quicksettings.TOGGLEABLE_TILE", Value = "true")]
public sealed class BoostTileService : TileService
{
    private IAudioBoostService? _boost;
    private ILogger<BoostTileService>? _logger;

    public override void OnCreate()
    {
        base.OnCreate();
        var services = MainApplication.Services;
        _boost = services.GetRequiredService<IAudioBoostService>();
        _logger = services.GetRequiredService<ILogger<BoostTileService>>();
    }

    public override void OnStartListening()
    {
        base.OnStartListening();
        _boost!.StateChanged += OnBoostStateChanged;
        UpdateTile(_boost.State);
    }

    public override void OnStopListening()
    {
        _boost!.StateChanged -= OnBoostStateChanged;
        base.OnStopListening();
    }

    public override void OnClick()
    {
        base.OnClick();
        var boost = _boost!;
        _logger!.LogInformation($"Tile clicked; the boost is {(boost.State.IsEnabled ? "on" : "off")}.");
        _ = boost.State.IsEnabled ? boost.DisableAsync() : boost.EnableAsync();
    }

    private void OnBoostStateChanged(object? sender, AudioBoostState state)
    {
        MainThread(() => UpdateTile(state));
    }

    private void UpdateTile(AudioBoostState state)
    {
        var tile = QsTile;
        if (tile is null)
        {
            return;
        }

        tile.State = state.Status switch
        {
            AudioBoostStatus.Unsupported => TileState.Unavailable,
            _ when state.IsEnabled => TileState.Active,
            _ => TileState.Inactive,
        };
        tile.Label = GetString(Resource.String.tile_label);
        tile.Subtitle = state.Status switch
        {
            AudioBoostStatus.Active => GetString(Resource.String.tile_active, new Java.Lang.String(state.GainPercent.ToString("0", CultureInfo.CurrentCulture))),
            AudioBoostStatus.Error => GetString(Resource.String.tile_error),
            AudioBoostStatus.Unsupported => GetString(Resource.String.tile_unsupported),
            _ => GetString(Resource.String.tile_off),
        };
        tile.UpdateTile();
    }

    private void MainThread(Action action)
    {
        new global::Android.OS.Handler(global::Android.OS.Looper.MainLooper!).Post(action);
    }
}
