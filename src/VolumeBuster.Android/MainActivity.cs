using Android.Content.PM;
using Android.OS;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using VolumeBuster.Audio;
using VolumeBuster.Diagnostics;

namespace VolumeBuster.Android;

[Activity(
    Label = "VolumeBuster",
    Theme = "@style/AppTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    // One narrow column of controls: turning the phone adds nothing. Android 16 ignores this on tablets and foldables.
    ScreenOrientation = ScreenOrientation.Portrait,
    ConfigurationChanges = ConfigChanges.Orientation
        | ConfigChanges.ScreenSize
        | ConfigChanges.UiMode
        | ConfigChanges.ScreenLayout
        | ConfigChanges.SmallestScreenSize
        | ConfigChanges.FontScale
        | ConfigChanges.Density
        | ConfigChanges.KeyboardHidden)]
public class MainActivity : AvaloniaMainActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);

        // Opening the app re-applies whatever the user left on (e.g. after a reboot); nothing starts on its own.
        _ = MainApplication.Services.GetRequiredService<IAudioBoostService>().RestoreAsync();
    }

    protected override void OnResume()
    {
        base.OnResume();
        MainApplication.Services.GetRequiredService<IAudioDiagnosticsProvider>().Refresh();
    }
}
