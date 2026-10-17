using Android.Content;
using Android.Content.Res;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VolumeX.Android.Audio;
using VolumeX.Android.Diagnostics;
using VolumeX.Android.Logging;
using VolumeX.Android.Services;
using VolumeX.Android.Settings;
using VolumeX.Audio;
using VolumeX.Diagnostics;
using VolumeX.Localization;
using VolumeX.Settings;
using VolumeX.Threading;
using VolumeX.ViewModels;

namespace VolumeX.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    public MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    /// <summary>
    /// The container shared by the activity, the foreground service and the tile: they all run in this process.
    /// </summary>
    public static IServiceProvider Services => App.Services
        ?? throw new InvalidOperationException("MainApplication has not been created yet.");

    public override void OnCreate()
    {
        ApplyLanguage(Resources!.Configuration!);
        App.Services ??= BuildServices(this);

        // Created eagerly: it starts the foreground service whenever the boost turns on, whoever turned it on.
        App.Services.GetRequiredService<BoostServiceLauncher>();

        base.OnCreate();
    }

    public override void OnConfigurationChanged(Configuration newConfig)
    {
        base.OnConfigurationChanged(newConfig);

        // The system language or the app's own one changed: the activity is re-created and reads its texts anew,
        // the service refreshes its notification.
        ApplyLanguage(newConfig);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }

    /// <summary>
    /// Takes the interface language from the configuration: the system languages, or the app's own language
    /// chosen in the system settings (Android 13+).
    /// </summary>
    private static void ApplyLanguage(Configuration configuration)
    {
        var locales = configuration.Locales;
        AppLanguages.Apply(AppLanguages.Resolve(Enumerable.Range(0, locales.Size()).Select(index => locales.Get(index)?.ToLanguageTag())));
    }

    private static ServiceProvider BuildServices(Context context)
    {
        var services = new ServiceCollection();
        services.AddLogging(logging => logging
            .AddProvider(new LogcatLoggerProvider())
            .SetMinimumLevel(LogLevel.Debug));

        services.AddSingleton(context);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(new AudioBoostServiceOptions());
        services.AddSingleton<ISettingsStore, SharedPreferencesSettingsStore>();
        services.AddSingleton<ISoundEffectFactory, AndroidSoundEffectFactory>();
        services.AddSingleton<IAudioBoostService, AudioBoostService>();
        services.AddSingleton<IAudioDiagnosticsProvider, AndroidAudioDiagnosticsProvider>();
        services.AddSingleton<IUiDispatcher, AvaloniaUiDispatcher>();
        services.AddSingleton<BoostServiceLauncher>();
        services.AddSingleton<MainViewModel>();

        return services.BuildServiceProvider();
    }
}
