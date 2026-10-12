using Microsoft.Extensions.Logging.Abstractions;
using VolumeBuster.Audio;
using VolumeBuster.Settings;
using VolumeBuster.Tests.Fakes;

namespace VolumeBuster.Tests;

/// <summary>
/// The equalizer and the bass level: the frequency response of the one effect that also carries the boost.
/// The fake effect has its bands at the ten-band frequencies, so a ten-band curve arrives unchanged.
/// </summary>
public sealed class AudioBoostServiceToneTests
{
    private FakeSoundEffectFactory _factory = null!;
    private InMemorySettingsStore _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new FakeSoundEffectFactory();
        _settings = new InMemorySettingsStore(new ApplicationSettings(GainPercent: 150, EqualizerPresetId: "rock"));
    }

    [Test]
    public async Task EnableEqualizer_AppliesTheCurveWithoutTouchingTheGain()
    {
        using var service = CreateService();

        await service.SetEqualizerEnabledAsync(true);

        var effect = _factory.Last!;
        Assert.That(effect.BandLevels, Is.EqualTo(EqualizerPresets.Find("rock").GainsDecibels));
        Assert.That(effect.AppliedGains, Is.Empty);
        Assert.That(effect.IsEnabled, Is.True);
        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(service.State.Tone.EffectName, Is.EqualTo(effect.Name));
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(_settings.Settings.IsEqualizerEnabled, Is.True);
    }

    [Test]
    public async Task EqualizerAndBoost_ShareOneEffect()
    {
        using var service = CreateService();

        await service.SetEqualizerEnabledAsync(true);
        await service.EnableAsync();

        Assert.That(_factory.Created, Has.Count.EqualTo(1));
        Assert.That(_factory.Last!.Gain, Is.EqualTo(GainCalculator.PercentToDecibels(150)));
        Assert.That(_factory.Last.BandLevels, Is.EqualTo(EqualizerPresets.Find("rock").GainsDecibels));
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Active));
    }

    [Test]
    public async Task DisableEqualizer_WithTheBoostOff_ReleasesTheEffect()
    {
        using var service = CreateService();
        await service.SetEqualizerEnabledAsync(true);

        await service.SetEqualizerEnabledAsync(false);

        Assert.That(_factory.Last!.IsDisposed, Is.True);
        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(service.State.IsAnyEnabled, Is.False);
        Assert.That(_settings.Settings.IsEqualizerEnabled, Is.False);
    }

    [Test]
    public async Task DisableEqualizer_FlattensTheCurveAndKeepsTheBoost()
    {
        using var service = CreateService();
        await service.EnableAsync();
        await service.SetEqualizerEnabledAsync(true);
        var gains = _factory.Last!.AppliedGains.Count;

        await service.SetEqualizerEnabledAsync(false);

        Assert.That(_factory.Last.IsDisposed, Is.False);
        Assert.That(_factory.Last.BandLevels, Has.All.EqualTo(0));
        Assert.That(_factory.Last.AppliedGains, Has.Count.EqualTo(gains));
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
    }

    [Test]
    public async Task DisableBoost_KeepsTheEqualizer()
    {
        using var service = CreateService();
        await service.EnableAsync();
        await service.SetEqualizerEnabledAsync(true);

        await service.DisableAsync();

        Assert.That(_factory.Last!.IsDisposed, Is.False);
        Assert.That(_factory.Last.Gain, Is.EqualTo(0));
        Assert.That(_factory.Last.IsEnabled, Is.True);
        Assert.That(_factory.Last.BandLevels, Is.EqualTo(EqualizerPresets.Find("rock").GainsDecibels));
        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Active));
    }

    [Test]
    public async Task BassAndEqualizer_AddUp()
    {
        using var service = CreateService();
        await service.SetBassAsync(6);

        await service.SetBassEnabledAsync(true);
        await service.SetEqualizerEnabledAsync(true);

        var rock = EqualizerPresets.Find("rock").GainsDecibels;
        var expected = EqualizerLayout.Ten.Frequencies
            .Select((frequency, band) => Math.Round(rock[band] + 6 * ToneCurve.BassWeight(frequency), 2))
            .ToArray();
        Assert.That(_factory.Last!.BandLevels, Is.EqualTo(expected));
        Assert.That(_factory.Created, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task BassDown_CutsOnlyTheLowEnd()
    {
        using var service = CreateService();
        await service.SetBassAsync(-9);

        await service.SetBassEnabledAsync(true);

        var levels = _factory.Last!.BandLevels;
        Assert.That(levels[0], Is.EqualTo(-9));
        Assert.That(levels[1], Is.LessThan(-6));
        Assert.That(levels.Skip(4), Has.All.EqualTo(0));
        Assert.That(_factory.Last.AppliedGains, Is.Empty);
        Assert.That(_factory.Last.IsEnabled, Is.True);
    }

    [Test]
    public async Task RaisingTheTone_ComesInSteps()
    {
        using var service = CreateService();
        await service.SetBassAsync(8);

        await service.SetBassEnabledAsync(true);

        // +8 dB at most 2 dB per step: four steps, each one higher than the last.
        var steps = _factory.Last!.AppliedLevels.Select(levels => levels[0]).ToArray();
        Assert.That(steps, Is.EqualTo(new double[] { 2, 4, 6, 8 }));
    }

    [Test]
    public async Task LoweringTheTone_IsImmediate()
    {
        using var service = CreateService();
        await service.SetBassAsync(9);
        await service.SetBassEnabledAsync(true);
        var steps = _factory.Last!.AppliedLevels.Count;

        await service.SetBassAsync(-9);

        Assert.That(_factory.Last.AppliedLevels, Has.Count.EqualTo(steps + 1));
        Assert.That(_factory.Last.BandLevels[0], Is.EqualTo(-9));
    }

    [Test]
    public async Task Restore_BringsTheToneAndTheGainInGradually()
    {
        _settings = new InMemorySettingsStore(new ApplicationSettings(GainPercent: 150, IsEnabled: true, IsBassEnabled: true, BassDecibels: 4));
        using var service = CreateService();

        await service.RestoreAsync();

        var effect = _factory.Last!;
        Assert.That(effect.Journal.Take(2), Is.EqualTo(new[] { "levels max 2", "levels max 4" }));
        Assert.That(effect.Journal.Skip(2), Has.All.StartWith("gain "));
        Assert.That(effect.AppliedGains, Is.Ordered.Ascending);
        Assert.That(effect.AppliedGains[0], Is.LessThanOrEqualTo(0.5));
    }

    [Test]
    public async Task ChangePreset_WhileOn_ReappliesTheCurve()
    {
        using var service = CreateService();
        await service.SetEqualizerEnabledAsync(true);

        await service.SetEqualizerPresetAsync("jazz");

        Assert.That(_factory.Last!.BandLevels, Is.EqualTo(EqualizerPresets.Find("jazz").GainsDecibels));
        Assert.That(service.State.Tone.EqualizerPresetId, Is.EqualTo("jazz"));
        Assert.That(_settings.Settings.EqualizerPresetId, Is.EqualTo("jazz"));
    }

    [Test]
    public async Task CustomCurve_IsAppliedSavedAndSurvivesAPresetRoundTrip()
    {
        using var service = CreateService();
        await service.SetEqualizerEnabledAsync(true);
        double[] own = [5, 3, -1, 10, 5, 0, 0, -3.5, 2, 1];

        await service.SetEqualizerGainsAsync(own);

        Assert.That(service.State.Tone.EqualizerPresetId, Is.EqualTo(EqualizerPresets.CustomId));
        Assert.That(service.State.Tone.EqualizerGainsDecibels, Is.EqualTo(own));
        Assert.That(_factory.Last!.BandLevels, Is.EqualTo(own));
        Assert.That(_settings.Settings.CustomEqualizerGainsDecibels, Is.EqualTo(own));

        await service.SetEqualizerPresetAsync("jazz");
        Assert.That(service.State.Tone.EqualizerGainsDecibels, Is.EqualTo(EqualizerPresets.Find("jazz").GainsDecibels));

        await service.SetEqualizerPresetAsync(EqualizerPresets.CustomId);
        Assert.That(service.State.Tone.EqualizerGainsDecibels, Is.EqualTo(own));
        Assert.That(_factory.Last.BandLevels, Is.EqualTo(own));
    }

    [Test]
    public void Constructor_RestoresTheCustomCurveWithinTheSliders()
    {
        _settings = new InMemorySettingsStore(new ApplicationSettings(
            EqualizerPresetId: EqualizerPresets.CustomId, CustomEqualizerGainsDecibels: [1, 2, 3, 4, 50, 0, 0, 0, 0, 0.3]));

        using var service = CreateService();

        Assert.That(service.State.Tone.EqualizerPresetId, Is.EqualTo(EqualizerPresets.CustomId));
        Assert.That(service.State.Tone.EqualizerGainsDecibels, Is.EqualTo(new double[] { 1, 2, 3, 4, 12, 0, 0, 0, 0, 0.5 }));
    }

    [Test]
    public void Constructor_CarriesACurveSavedForOtherBandsOver()
    {
        // The five-band curve of an earlier version, read with the default ten bands.
        _settings = new InMemorySettingsStore(new ApplicationSettings(
            EqualizerPresetId: EqualizerPresets.CustomId, CustomEqualizerGainsDecibels: [6, 0, -6, 0, 6]));

        using var service = CreateService();

        var gains = service.State.Tone.EqualizerGainsDecibels;
        Assert.That(gains, Has.Count.EqualTo(10));
        Assert.That(new[] { gains[1], gains[3], gains[5], gains[7], gains[9] }, Is.EqualTo(new double[] { 6, 0, -6, 0, 6 }));
    }

    [Test]
    public async Task BandCount_KeepsThePresetOnTheNewBands()
    {
        using var service = CreateService();
        await service.SetEqualizerEnabledAsync(true);

        await service.SetEqualizerBandCountAsync(5);

        Assert.That(service.State.Tone.EqualizerLayout, Is.SameAs(EqualizerLayout.Five));
        Assert.That(service.State.Tone.EqualizerGainsDecibels, Is.EqualTo(new double[] { 5, 3, -1, 3, 5 }));
        Assert.That(service.State.Tone.EqualizerPresetId, Is.EqualTo("rock"));
        Assert.That(_settings.Settings.EqualizerBandCount, Is.EqualTo(5));

        // The effect hears the same curve, read between the five bands.
        var expected = EqualizerLayout.Ten.Frequencies
            .Select(frequency => Math.Round(EqualizerCurve.DecibelsAt(EqualizerLayout.Five, [5, 3, -1, 3, 5], frequency), 2));
        Assert.That(_factory.Last!.BandLevels, Is.EqualTo(expected));
    }

    [Test]
    public async Task BandCount_ResamplesTheOwnCurveAndSavesIt()
    {
        using var service = CreateService();
        await service.SetEqualizerGainsAsync([0, 6, 0, 0, 0, -6, 0, 0, 0, 0]);

        await service.SetEqualizerBandCountAsync(5);

        Assert.That(service.State.Tone.EqualizerGainsDecibels, Is.EqualTo(new double[] { 6, 0, -6, 0, 0 }));
        Assert.That(_settings.Settings.CustomEqualizerGainsDecibels, Is.EqualTo(new double[] { 6, 0, -6, 0, 0 }));
        Assert.That(_factory.Created, Is.Empty);
    }

    [Test]
    public async Task ChangePresetAndBass_WhileOff_OnlyRemembersThem()
    {
        using var service = CreateService();

        await service.SetEqualizerPresetAsync("pop");
        await service.SetBassAsync(-5);

        Assert.That(_factory.Created, Is.Empty);
        Assert.That(service.State.Tone.EqualizerPresetId, Is.EqualTo("pop"));
        Assert.That(service.State.Tone.BassDecibels, Is.EqualTo(-5));
        Assert.That(_settings.Settings.EqualizerPresetId, Is.EqualTo("pop"));
        Assert.That(_settings.Settings.BassDecibels, Is.EqualTo(-5));
    }

    [Test]
    public async Task FlatCurveAt100Percent_LeavesTheEffectSwitchedOff()
    {
        _settings = new InMemorySettingsStore(new ApplicationSettings(GainPercent: 100));
        using var service = CreateService();

        await service.EnableAsync();
        await service.SetEqualizerEnabledAsync(true);

        Assert.That(_factory.Last!.IsEnabled, Is.False);
        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Active));
    }

    [Test]
    [SetUICulture("ru-RU")]
    public async Task LevelOnlyEffect_TurnsTheEqualizerOffWithAReasonAndKeepsTheBoost()
    {
        _factory.Configure = effect => effect.BandFrequencies = [];
        using var service = CreateService();
        await service.EnableAsync();

        await service.SetEqualizerEnabledAsync(true);

        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Unsupported));
        Assert.That(service.State.Tone.IsEqualizerEnabled, Is.False);
        Assert.That(service.State.Tone.Message, Does.Contain("только громкость"));
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(_factory.Last!.IsDisposed, Is.False);
        Assert.That(_settings.Settings.IsEqualizerEnabled, Is.False);
    }

    [Test]
    public async Task LevelOnlyEffect_AloneIsReleasedAgain()
    {
        _factory.Configure = effect => effect.BandFrequencies = [];
        using var service = CreateService();

        await service.SetBassEnabledAsync(true);

        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Unsupported));
        Assert.That(service.State.Tone.IsBassEnabled, Is.False);
        Assert.That(_factory.Last!.IsDisposed, Is.True);
    }

    [Test]
    public async Task UnsupportedDevice_FlipsTheSwitchesBackAndLeavesTheBoostAlone()
    {
        _factory.Exception = new EffectUnavailableException("No global effects here.");
        using var service = CreateService();

        await service.SetEqualizerEnabledAsync(true);

        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Unsupported));
        Assert.That(service.State.Tone.IsEqualizerEnabled, Is.False);
        Assert.That(service.State.Tone.Message, Is.EqualTo("No global effects here."));
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(service.State.Message, Is.Null);
        Assert.That(_settings.Settings.IsEqualizerEnabled, Is.False);
    }

    [Test]
    public async Task EffectControlledByAnotherApp_ReportsError()
    {
        _factory.Exception = new InvalidOperationException("Another app controls the effect.");
        using var service = CreateService();

        await service.SetBassEnabledAsync(true);

        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Error));
        Assert.That(service.State.Tone.IsBassEnabled, Is.False);
        Assert.That(service.State.Tone.Message, Is.EqualTo("Another app controls the effect."));
    }

    [Test]
    public async Task Verify_AfterLosingControl_RecreatesTheEffectWithEverything()
    {
        using var service = CreateService();
        await service.EnableAsync();
        await service.SetEqualizerEnabledAsync(true);
        var first = _factory.Last!;
        first.HasControl = false;

        await service.VerifyAsync();

        Assert.That(first.IsDisposed, Is.True);
        Assert.That(_factory.Created, Has.Count.EqualTo(2));
        Assert.That(_factory.Last!.Gain, Is.EqualTo(GainCalculator.PercentToDecibels(150)));
        Assert.That(_factory.Last.BandLevels, Is.EqualTo(EqualizerPresets.Find("rock").GainsDecibels));
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Active));
    }

    [Test]
    public async Task Verify_KeepsTheEqualizerOnWhileTheEffectIsGone()
    {
        using var service = CreateService();
        await service.SetEqualizerEnabledAsync(true);
        _factory.Last!.HasControl = false;
        _factory.Exception = new EffectUnavailableException("Audio server is restarting.");

        await service.VerifyAsync();

        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Error));
        Assert.That(service.State.Tone.IsEqualizerEnabled, Is.True);
        Assert.That(_settings.Settings.IsEqualizerEnabled, Is.True);

        _factory.Exception = null;
        await service.VerifyAsync();

        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(_factory.Last.BandLevels, Is.EqualTo(EqualizerPresets.Find("rock").GainsDecibels));
    }

    [Test]
    public async Task DisableAll_SwitchesEverythingOffAndReleasesTheEffect()
    {
        using var service = CreateService();
        await service.EnableAsync();
        await service.SetBassEnabledAsync(true);
        await service.SetEqualizerEnabledAsync(true);

        await service.DisableAllAsync();

        Assert.That(_factory.Created, Has.Count.EqualTo(1));
        Assert.That(_factory.Last!.IsDisposed, Is.True);
        Assert.That(service.State.IsAnyEnabled, Is.False);
        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(_settings.Settings.IsEnabled, Is.False);
        Assert.That(_settings.Settings.IsEqualizerEnabled, Is.False);
        Assert.That(_settings.Settings.IsBassEnabled, Is.False);
    }

    [Test]
    public async Task Restore_SwitchesOnWhatTheUserLeftOn()
    {
        _settings = new InMemorySettingsStore(new ApplicationSettings(
            GainPercent: 130, IsEnabled: true, IsEqualizerEnabled: true, EqualizerPresetId: "jazz", IsBassEnabled: true, BassDecibels: 3));
        using var service = CreateService();

        await service.RestoreAsync();

        Assert.That(_factory.Created, Has.Count.EqualTo(1));
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(service.State.Tone.IsEqualizerEnabled, Is.True);
        Assert.That(service.State.Tone.IsBassEnabled, Is.True);
        Assert.That(_factory.Last!.Gain, Is.EqualTo(GainCalculator.PercentToDecibels(130)));
        Assert.That(_factory.Last.BandLevels[9], Is.EqualTo(5));
    }

    [Test]
    public void Constructor_StartsOffWithTheSavedLayoutPresetAndBass()
    {
        _settings = new InMemorySettingsStore(new ApplicationSettings(
            IsEqualizerEnabled: true, EqualizerPresetId: "jazz", BassDecibels: 40, EqualizerBandCount: 7));

        using var service = CreateService();

        Assert.That(service.State.Tone.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(service.State.Tone.IsEqualizerEnabled, Is.False);
        Assert.That(service.State.Tone.EqualizerLayout, Is.SameAs(EqualizerLayout.Seven));
        Assert.That(service.State.Tone.EqualizerGainsDecibels, Is.EqualTo(EqualizerPresets.Find("jazz").GainsFor(EqualizerLayout.Seven)));
        Assert.That(service.State.Tone.BassDecibels, Is.EqualTo(ToneCurve.MaxBassDecibels));
        Assert.That(_factory.Created, Is.Empty);
    }

    private AudioBoostService CreateService()
    {
        var options = new AudioBoostServiceOptions { RampStepDelay = TimeSpan.Zero, HealthCheckInterval = null };
        return new AudioBoostService(_factory, _settings, options, TimeProvider.System, NullLogger<AudioBoostService>.Instance);
    }
}
