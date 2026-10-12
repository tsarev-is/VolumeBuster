using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using VolumeBuster.ViewModels;
using VolumeBuster.Views;

namespace VolumeBuster;

public partial class App : Application
{
    /// <summary>
    /// Container built by the platform host before Avalonia starts.
    /// </summary>
    public static IServiceProvider? Services { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = Services ?? throw new InvalidOperationException("App.Services must be set by the host.");

        if (ApplicationLifetime is IActivityApplicationLifetime activityLifetime)
        {
            // The view model is a singleton: a recreated activity gets the same state.
            activityLifetime.MainViewFactory = () => new MainView { DataContext = services.GetRequiredService<MainViewModel>() };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
