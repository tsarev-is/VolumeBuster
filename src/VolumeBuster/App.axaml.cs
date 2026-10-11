using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VolumeBuster.Platform;
using VolumeBuster.ViewModels;
using VolumeBuster.Views;

namespace VolumeBuster;

public partial class App : Application
{
    /// <summary>
    /// Volume boost provided by the Android host before Avalonia starts.
    /// </summary>
    public static IVolumeBooster? Booster { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var booster = Booster ?? throw new InvalidOperationException("App.Booster must be set by the host.");
        var shell = new MainViewModel(booster);

        if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime)
        {
            activityLifetime.MainViewFactory = () => new MainView { DataContext = shell };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
