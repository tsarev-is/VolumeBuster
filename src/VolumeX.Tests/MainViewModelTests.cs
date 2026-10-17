using Microsoft.Extensions.Logging.Abstractions;
using VolumeX.Audio;
using VolumeX.Diagnostics;
using VolumeX.Settings;
using VolumeX.Tests.Fakes;
using VolumeX.ViewModels;

namespace VolumeX.Tests;

/// <summary>
/// The screen's behaviour; its texts are checked in Russian, see <see cref="LocalizationTests"/> for the others.
/// </summary>
[SetCulture("ru-RU")]
[SetUICulture("ru-RU")]
public sealed class MainViewModelTests
{
    private FakeSoundEffectFactory _factory = null!;
    private InMemorySettingsStore _settings = null!;
    private FakeDiagnosticsProvider _diagnostics = null!;
    private AudioBoostService _service = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new FakeSoundEffectFactory();
        _settings = new InMemorySettingsStore(new ApplicationSettings(GainPercent: 125));
        _diagnostics = new FakeDiagnosticsProvider();
        _service = CreateService();
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [Test]
    public void Constructor_ShowsTheSavedGainAndAnOffSwitch()
    {
        using var viewModel = CreateViewModel();

        Assert.That(viewModel.GainPercent, Is.EqualTo(125));
        Assert.That(viewModel.GainPercentText, Is.EqualTo("125\u00A0%"));
        Assert.That(viewModel.IsBoostEnabled, Is.False);
        Assert.That(viewModel.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(viewModel.IsWarningVisible, Is.False);
        Assert.That(_factory.Created, Is.Empty);
    }

    [Test]
    public void SwitchOn_EnablesTheBoostAndReportsTheRealStatus()
    {
        using var viewModel = CreateViewModel();

        viewModel.IsBoostEnabled = true;

        Assert.That(_service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(viewModel.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(viewModel.IsBoostProblemVisible, Is.False);
        Assert.That(viewModel.EffectText, Does.StartWith(_factory.Last!.Name));
    }

    [Test]
    public void SwitchOff_RestoresTheOriginalSound()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsBoostEnabled = true;

        viewModel.IsBoostEnabled = false;

        Assert.That(_factory.Last!.IsDisposed, Is.True);
        Assert.That(viewModel.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(viewModel.EffectText, Is.EqualTo("—"));
    }

    [Test]
    public void FirstEnable_ShowsTheWarningUntilAcknowledged()
    {
        using var viewModel = CreateViewModel();

        viewModel.IsBoostEnabled = true;

        Assert.That(viewModel.IsWarningVisible, Is.True);

        viewModel.AcknowledgeWarningCommand.Execute(null);

        Assert.That(viewModel.IsWarningVisible, Is.False);
        Assert.That(_settings.Settings.IsWarningAcknowledged, Is.True);

        viewModel.IsBoostEnabled = false;
        viewModel.IsBoostEnabled = true;

        Assert.That(viewModel.IsWarningVisible, Is.False);
    }

    [TestCase(300, 150)]
    [TestCase(50, 100)]
    [TestCase(117, 115)]
    [TestCase(118, 120)]
    public void Slider_StaysWithinTheRangeAndOnSteps(double requested, double expected)
    {
        using var viewModel = CreateViewModel();

        viewModel.GainPercent = requested;

        Assert.That(viewModel.GainPercent, Is.EqualTo(expected));
        Assert.That(_service.State.GainPercent, Is.EqualTo(expected));
    }

    [Test]
    public void Slider_WhileActive_ChangesTheAppliedGain()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsBoostEnabled = true;

        viewModel.GainPercent = 150;

        Assert.That(_factory.Last!.Gain, Is.EqualTo(GainCalculator.PercentToDecibels(150)));
        Assert.That(viewModel.GainDecibelsText, Does.StartWith("+10"));
    }

    [Test]
    public void UnsupportedDevice_FlipsTheSwitchBackAndExplainsWhy()
    {
        _factory.Exception = new EffectUnavailableException("No global effects here.");
        using var viewModel = CreateViewModel();

        viewModel.IsBoostEnabled = true;

        Assert.That(viewModel.IsBoostEnabled, Is.False);
        Assert.That(viewModel.Status, Is.EqualTo(AudioBoostStatus.Unsupported));
        Assert.That(viewModel.IsBoostProblemVisible, Is.True);
        Assert.That(viewModel.BoostProblemText, Is.EqualTo("No global effects here."));
    }

    [Test]
    public void FailingService_ShowsAnErrorInsteadOfCrashing()
    {
        using var failing = new ThrowingAudioBoostService(new InvalidOperationException("Audio server is gone."));
        using var viewModel = new MainViewModel(failing, _settings, _diagnostics, new ImmediateUiDispatcher(), NullLogger<MainViewModel>.Instance);

        viewModel.IsBoostEnabled = true;

        Assert.That(viewModel.Status, Is.EqualTo(AudioBoostStatus.Error));
        Assert.That(viewModel.IsBoostEnabled, Is.False);
        Assert.That(viewModel.BoostProblemText, Does.Contain("Audio server is gone."));
    }

    [Test]
    public async Task TurningOffElsewhere_UpdatesTheSwitchWithoutReEnabling()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsBoostEnabled = true;

        // The notification button and the Quick Settings tile talk to the service directly.
        await _service.DisableAsync();

        Assert.That(viewModel.IsBoostEnabled, Is.False);
        Assert.That(viewModel.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(_factory.Created, Has.Count.EqualTo(1));
    }

    [Test]
    public void Diagnostics_FollowTheMediaVolume()
    {
        using var viewModel = CreateViewModel();

        _diagnostics.Set(_diagnostics.Current with { MediaVolume = 6 });

        Assert.That(viewModel.MediaVolumeText, Is.EqualTo("6 из 16"));
    }

    [Test]
    public void Diagnostics_FollowTheRoute()
    {
        using var viewModel = CreateViewModel();

        _diagnostics.Set(_diagnostics.Current with { OutputRoute = "Динамик телефона" });

        Assert.That(viewModel.OutputRouteText, Is.EqualTo("Динамик телефона"));
    }

    [Test]
    public void Dispose_StopsListeningToTheService()
    {
        var viewModel = CreateViewModel();

        viewModel.Dispose();
        _service.EnableAsync().GetAwaiter().GetResult();

        Assert.That(viewModel.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(viewModel.IsBoostEnabled, Is.False);
    }

    [Test]
    public void BassSwitch_TurnsOnOnlyTheBass()
    {
        using var viewModel = CreateViewModel();
        viewModel.BassDecibels = 6;

        viewModel.IsBassEnabled = true;

        Assert.That(_service.State.Tone.IsBassEnabled, Is.True);
        Assert.That(_service.State.IsEnabled, Is.False);
        Assert.That(viewModel.IsBoostEnabled, Is.False);
        Assert.That(viewModel.IsEqualizerEnabled, Is.False);
        Assert.That(viewModel.ToneStatus, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(viewModel.BassText, Is.EqualTo("+6"));
        Assert.That(viewModel.IsWarningVisible, Is.True);
    }

    [Test]
    public void EqualizerSwitchAndPreset_ReachTheEngine()
    {
        using var viewModel = CreateViewModel();

        viewModel.IsEqualizerEnabled = true;
        viewModel.SelectedPreset = EqualizerPresets.Find("jazz");

        Assert.That(_service.State.Tone.IsEqualizerEnabled, Is.True);
        Assert.That(_service.State.Tone.EqualizerPresetId, Is.EqualTo("jazz"));
        Assert.That(_factory.Last!.BandLevels, Is.EqualTo(EqualizerPresets.Find("jazz").GainsDecibels));
        Assert.That(viewModel.EqualizerGains, Is.EqualTo(new double[] { 4, 4, 3, 2, -0.5, -2, 0, 2.5, 4, 5 }));
    }

    [Test]
    public void MovingABand_SwitchesToTheOwnCurveAndReachesTheEngine()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsEqualizerEnabled = true;
        viewModel.SelectedPreset = EqualizerPresets.Find("rock");

        double[] own = [5, 5, 4, 3, 0.5, -1, 1, 8, 4.5, 5];
        viewModel.EqualizerGains = own;

        Assert.That(viewModel.SelectedPreset.Id, Is.EqualTo(EqualizerPresets.CustomId));
        Assert.That(_service.State.Tone.EqualizerPresetId, Is.EqualTo(EqualizerPresets.CustomId));
        Assert.That(_service.State.Tone.EqualizerGainsDecibels, Is.EqualTo(own));
        Assert.That(_factory.Last!.BandLevels, Is.EqualTo(own));
        Assert.That(viewModel.EqualizerGains, Is.EqualTo(own));

        viewModel.SelectedPreset = viewModel.PresetOptions.First(preset => preset.Id == "pop");

        Assert.That(viewModel.EqualizerGains, Is.EqualTo(EqualizerPresets.Find("pop").GainsDecibels));

        viewModel.SelectedPreset = viewModel.PresetOptions.Last();

        Assert.That(viewModel.EqualizerGains, Is.EqualTo(own));
    }

    [Test]
    public void PresetOptions_EndWithTheOwnCurve()
    {
        using var viewModel = CreateViewModel();

        Assert.That(viewModel.PresetOptions.Select(preset => preset.Name), Is.EqualTo(
            EqualizerPresets.All.Select(preset => preset.Name).Append(EqualizerPresets.CustomName)));
        Assert.That(viewModel.EqualizerLabels, Is.EqualTo(new[] { "31", "63", "125", "250", "500", "1k", "2k", "4k", "8k", "16k" }));
        Assert.That(viewModel.IsTenBands, Is.True);
    }

    [Test]
    public void BandCount_ChangesTheBandsAndKeepsThePreset()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsEqualizerEnabled = true;
        viewModel.SelectedPreset = EqualizerPresets.Find("rock");

        viewModel.IsFiveBands = true;

        Assert.That(viewModel.EqualizerBandCount, Is.EqualTo(5));
        Assert.That(viewModel.IsTenBands, Is.False);
        Assert.That(viewModel.EqualizerLabels, Is.EqualTo(new[] { "63", "250", "1k", "4k", "16k" }));
        Assert.That(viewModel.EqualizerGains, Is.EqualTo(new double[] { 5, 3, -1, 3, 5 }));
        Assert.That(viewModel.SelectedPreset.Id, Is.EqualTo("rock"));
        Assert.That(_service.State.Tone.EqualizerLayout, Is.SameAs(EqualizerLayout.Five));
        Assert.That(_settings.Settings.EqualizerBandCount, Is.EqualTo(5));
    }

    [Test]
    public void BandCount_CarriesTheOwnCurveOver()
    {
        using var viewModel = CreateViewModel();
        viewModel.EqualizerGains = [0, 6, 0, 0, 0, -6, 0, 0, 0, 0];

        viewModel.IsFiveBands = true;

        // The five bands sit on every other one of the ten.
        Assert.That(viewModel.EqualizerGains, Is.EqualTo(new double[] { 6, 0, -6, 0, 0 }));
        Assert.That(viewModel.SelectedPreset.Id, Is.EqualTo(EqualizerPresets.CustomId));
        Assert.That(_service.State.Tone.EqualizerGainsDecibels, Is.EqualTo(new double[] { 6, 0, -6, 0, 0 }));
    }

    [Test]
    public void Header_NamesTheOutputDevice()
    {
        using var viewModel = CreateViewModel();

        _diagnostics.Set(_diagnostics.Current with { OutputDevice = "Динамик телефона" });

        Assert.That(viewModel.OutputDeviceText, Is.EqualTo("Динамик телефона"));
    }

    [Test]
    public void SwitchingOneOff_LeavesTheOthersOn()
    {
        using var viewModel = CreateViewModel();
        viewModel.IsBoostEnabled = true;
        viewModel.IsBassEnabled = true;
        viewModel.IsEqualizerEnabled = true;

        viewModel.IsBassEnabled = false;

        Assert.That(viewModel.IsBoostEnabled, Is.True);
        Assert.That(viewModel.IsEqualizerEnabled, Is.True);
        Assert.That(_service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(_service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(_service.State.Tone.IsBassEnabled, Is.False);
    }

    [TestCase(5.4, 5.5)]
    [TestCase(5.2, 5)]
    [TestCase(-20, -9)]
    [TestCase(300, 9)]
    public void BassSlider_StaysWithinTheRangeAndOnSteps(double requested, double expected)
    {
        using var viewModel = CreateViewModel();

        viewModel.BassDecibels = requested;

        Assert.That(viewModel.BassDecibels, Is.EqualTo(expected));
        Assert.That(_service.State.Tone.BassDecibels, Is.EqualTo(expected));
    }

    [TestCase(-4.5, "−4,5")]
    [TestCase(9, "+9")]
    [TestCase(0, "0")]
    public void BassText_ShowsHalfSteps(double decibels, string expected)
    {
        using var viewModel = CreateViewModel();

        viewModel.BassDecibels = decibels;

        Assert.That(viewModel.BassText, Is.EqualTo(expected));
    }

    [Test]
    public void UnsupportedEqualizer_FlipsTheSwitchBackAndExplainsWhy()
    {
        _factory.Exception = new EffectUnavailableException("No equalizer here.");
        using var viewModel = CreateViewModel();

        viewModel.IsEqualizerEnabled = true;

        Assert.That(viewModel.IsEqualizerEnabled, Is.False);
        Assert.That(viewModel.ToneStatus, Is.EqualTo(AudioBoostStatus.Unsupported));
        Assert.That(viewModel.IsToneProblemVisible, Is.True);
        Assert.That(viewModel.ToneProblemText, Is.EqualTo("No equalizer here."));
        Assert.That(viewModel.IsBoostProblemVisible, Is.False);
    }

    private AudioBoostService CreateService()
    {
        var options = new AudioBoostServiceOptions { RampStepDelay = TimeSpan.Zero, HealthCheckInterval = null };
        return new AudioBoostService(_factory, _settings, options, TimeProvider.System, NullLogger<AudioBoostService>.Instance);
    }

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(_service, _settings, _diagnostics, new ImmediateUiDispatcher(), NullLogger<MainViewModel>.Instance);
    }
}
