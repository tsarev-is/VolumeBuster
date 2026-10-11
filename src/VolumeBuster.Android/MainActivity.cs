using Android.Content.PM;
using Avalonia.Android;

namespace VolumeBuster.Android;

[Activity(
    Label = "VolumeBuster",
    Theme = "@style/AppTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
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
}
