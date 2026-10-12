using Microsoft.Extensions.Logging.Abstractions;
using VolumeBuster.Audio;
using VolumeBuster.Settings;
using VolumeBuster.Tests.Fakes;

namespace VolumeBuster.Tests;

public sealed class AudioBoostServiceTests
{
    private FakeSoundEffectFactory _factory = null!;
    private InMemorySettingsStore _settings = null!;

    [SetUp]
    public void SetUp()
    {
        _factory = new FakeSoundEffectFactory();
        _settings = new InMemorySettingsStore(new ApplicationSettings(GainPercent: 150));
    }

    [Test]
    public async Task Enable_AttachesTheEffectAndRampsUpToTheSelectedGain()
    {
        using var service = CreateService();

        await service.EnableAsync();

        var effect = _factory.Last!;
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(service.State.IsEnabled, Is.True);
        Assert.That(service.State.EffectName, Is.EqualTo(effect.Name));
        Assert.That(effect.IsEnabled, Is.True);
        Assert.That(effect.Gain, Is.EqualTo(GainCalculator.PercentToDecibels(150)));
        Assert.That(effect.AppliedGains, Is.Ordered.Ascending);
        Assert.That(effect.AppliedGains[0], Is.LessThanOrEqualTo(0.5));
    }

    [Test]
    public async Task Enable_At100Percent_KeepsTheEffectSwitchedOff()
    {
        _settings = new InMemorySettingsStore(new ApplicationSettings(GainPercent: 100));
        using var service = CreateService();

        await service.EnableAsync();

        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(_factory.Last!.IsEnabled, Is.False);
        Assert.That(_factory.Last.AppliedGains, Is.Empty);
    }

    [Test]
    public async Task Enable_Twice_CreatesOneEffect()
    {
        using var service = CreateService();

        await service.EnableAsync();
        await service.EnableAsync();

        Assert.That(_factory.Created, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task Disable_ReleasesTheEffectAndRestoresTheOriginalState()
    {
        using var service = CreateService();
        await service.EnableAsync();

        await service.DisableAsync();

        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(service.State.IsEnabled, Is.False);
        Assert.That(service.State.EffectName, Is.Null);
        Assert.That(_factory.Last!.IsDisposed, Is.True);
        Assert.That(_factory.Last.IsEnabled, Is.False);
    }

    [Test]
    public async Task RepeatedEnableAndDisable_UsesAFreshEffectEachTimeAndLeavesNoneAttached()
    {
        using var service = CreateService();

        for (var i = 0; i < 5; i++)
        {
            await service.EnableAsync();
            await service.DisableAsync();
        }

        Assert.That(_factory.Created, Has.Count.EqualTo(5));
        Assert.That(_factory.Created, Has.All.Matches<FakeSoundEffect>(effect => effect.IsDisposed));
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Disabled));
    }

    [Test]
    public async Task SetGain_WhileActive_RampsUpAndDropsAtOnce()
    {
        _settings = new InMemorySettingsStore(new ApplicationSettings(GainPercent: 110));
        using var service = CreateService();
        await service.EnableAsync();
        var effect = _factory.Last!;

        await service.SetGainAsync(150);
        var afterIncrease = effect.AppliedGains.Count;
        await service.SetGainAsync(105);

        Assert.That(effect.Gain, Is.EqualTo(GainCalculator.PercentToDecibels(105)));
        Assert.That(effect.AppliedGains.Count - afterIncrease, Is.EqualTo(1));
        Assert.That(service.State.GainPercent, Is.EqualTo(105));
    }

    [Test]
    public async Task SetGain_To100Percent_SwitchesTheEffectOffButKeepsItAttached()
    {
        using var service = CreateService();
        await service.EnableAsync();

        await service.SetGainAsync(100);

        Assert.That(_factory.Last!.IsEnabled, Is.False);
        Assert.That(_factory.Last.IsDisposed, Is.False);
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
    }

    [Test]
    public async Task SetGain_WhileDisabled_OnlyRemembersThePercentage()
    {
        using var service = CreateService();

        await service.SetGainAsync(125);

        Assert.That(_factory.Created, Is.Empty);
        Assert.That(service.State.GainPercent, Is.EqualTo(125));
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Disabled));
    }

    [TestCase(1000, 150)]
    [TestCase(-5, 100)]
    [TestCase(112, 110)]
    [TestCase(double.NaN, 100)]
    public async Task SetGain_KeepsTheSliderBoundaries(double requested, double expected)
    {
        using var service = CreateService();

        await service.SetGainAsync(requested);

        Assert.That(service.State.GainPercent, Is.EqualTo(expected));
        Assert.That(_settings.Settings.GainPercent, Is.EqualTo(expected));
    }

    [Test]
    public async Task EnableAndSetGain_PersistTheChoice()
    {
        using var service = CreateService();

        await service.EnableAsync();
        await service.SetGainAsync(130);

        Assert.That(_settings.Settings.IsEnabled, Is.True);
        Assert.That(_settings.Settings.GainPercent, Is.EqualTo(130));

        await service.DisableAsync();

        Assert.That(_settings.Settings.IsEnabled, Is.False);
        Assert.That(_settings.Settings.GainPercent, Is.EqualTo(130));
    }

    [Test]
    public void Constructor_StartsDisabledWithTheSavedGainNormalized()
    {
        _settings = new InMemorySettingsStore(new ApplicationSettings(GainPercent: 999, IsEnabled: true));

        using var service = CreateService();

        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(service.State.IsEnabled, Is.False);
        Assert.That(service.State.GainPercent, Is.EqualTo(GainCalculator.MaxPercent));
        Assert.That(_factory.Created, Is.Empty);
    }

    [Test]
    public async Task Restore_EnablesTheBoostTheUserLeftOn()
    {
        _settings = new InMemorySettingsStore(new ApplicationSettings(GainPercent: 140, IsEnabled: true));
        using var service = CreateService();

        await service.RestoreAsync();

        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
        Assert.That(_factory.Last!.Gain, Is.EqualTo(GainCalculator.PercentToDecibels(140)));
    }

    [Test]
    public async Task Restore_LeavesTheBoostOffWhenTheUserTurnedItOff()
    {
        using var service = CreateService();

        await service.RestoreAsync();

        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Disabled));
        Assert.That(_factory.Created, Is.Empty);
    }

    [Test]
    public async Task Enable_OnAnUnsupportedDevice_ReportsUnsupportedAndStaysOff()
    {
        _factory.Exception = new EffectUnavailableException("No global effects here.");
        using var service = CreateService();

        await service.EnableAsync();

        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Unsupported));
        Assert.That(service.State.IsEnabled, Is.False);
        Assert.That(service.State.Message, Is.EqualTo("No global effects here."));
        Assert.That(_settings.Settings.IsEnabled, Is.False);
    }

    [Test]
    public async Task Enable_WhenAnotherAppControlsTheEffect_ReportsError()
    {
        _factory.Exception = new InvalidOperationException("Another app controls the effect.");
        using var service = CreateService();

        await service.EnableAsync();

        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Error));
        Assert.That(service.State.IsEnabled, Is.False);
        Assert.That(service.State.Message, Is.EqualTo("Another app controls the effect."));
    }

    [Test]
    public async Task Enable_WhenTheAudioServerRejectsTheEffect_ReleasesItAndReportsError()
    {
        _factory.Configure = effect => effect.EnableException = new InvalidOperationException("Rejected.");
        using var service = CreateService();

        await service.EnableAsync();

        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Error));
        Assert.That(service.State.IsEnabled, Is.False);
        Assert.That(_factory.Last!.IsDisposed, Is.True);
        Assert.That(_settings.Settings.IsEnabled, Is.False);
    }

    [Test]
    public async Task SetGain_WhenTheEffectDies_ReleasesItButStaysOnForRecovery()
    {
        _settings = new InMemorySettingsStore(new ApplicationSettings(GainPercent: 110));
        using var service = CreateService();
        await service.EnableAsync();
        _factory.Last!.SetGainException = new InvalidOperationException("Dead object.");

        await service.SetGainAsync(150);

        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Error));
        Assert.That(service.State.IsEnabled, Is.True);
        Assert.That(_factory.Last.IsDisposed, Is.True);
    }

    [Test]
    public async Task Verify_AfterLosingControl_RecreatesTheEffect()
    {
        using var service = CreateService();
        await service.EnableAsync();
        var first = _factory.Last!;
        first.HasControl = false;

        await service.VerifyAsync();

        Assert.That(first.IsDisposed, Is.True);
        Assert.That(_factory.Created, Has.Count.EqualTo(2));
        Assert.That(_factory.Last!.Gain, Is.EqualTo(GainCalculator.PercentToDecibels(150)));
        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
    }

    [Test]
    public async Task Verify_KeepsRetryingUntilTheEffectComesBack()
    {
        using var service = CreateService();
        await service.EnableAsync();
        _factory.Last!.HasControl = false;
        _factory.Exception = new EffectUnavailableException("Audio server is restarting.");

        await service.VerifyAsync();

        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Error));
        Assert.That(service.State.IsEnabled, Is.True);
        Assert.That(_settings.Settings.IsEnabled, Is.True);

        _factory.Exception = null;
        await service.VerifyAsync();

        Assert.That(service.State.Status, Is.EqualTo(AudioBoostStatus.Active));
    }

    [Test]
    public async Task Verify_LeavesAHealthyEffectAlone()
    {
        using var service = CreateService();
        await service.EnableAsync();

        await service.VerifyAsync();

        Assert.That(_factory.Created, Has.Count.EqualTo(1));
    }

    [Test]
    public async Task StateChanged_ReportsEveryTransition()
    {
        using var service = CreateService();
        var states = new List<AudioBoostStatus>();
        service.StateChanged += (_, state) => states.Add(state.Status);

        await service.EnableAsync();
        await service.DisableAsync();

        Assert.That(states, Is.EqualTo(new[] { AudioBoostStatus.Active, AudioBoostStatus.Disabled }));
    }

    [Test]
    public async Task GetStatus_ReturnsTheCurrentState()
    {
        using var service = CreateService();
        await service.EnableAsync();

        var state = await service.GetStatusAsync();

        Assert.That(state, Is.SameAs(service.State));
    }

    [Test]
    public async Task Dispose_ReleasesTheEffectButKeepsThePreference()
    {
        var service = CreateService();
        await service.EnableAsync();

        service.Dispose();

        Assert.That(_factory.Last!.IsDisposed, Is.True);
        Assert.That(_settings.Settings.IsEnabled, Is.True);
        Assert.That(() => service.EnableAsync(), Throws.InstanceOf<ObjectDisposedException>());
        Assert.That(() => service.DisableAsync(), Throws.InstanceOf<ObjectDisposedException>());
        Assert.That(() => service.SetGainAsync(120), Throws.InstanceOf<ObjectDisposedException>());
    }

    [Test]
    public void Dispose_Twice_IsHarmless()
    {
        var service = CreateService();

        service.Dispose();

        Assert.That(service.Dispose, Throws.Nothing);
    }

    [Test]
    public async Task Ramp_WaitsBetweenSteps()
    {
        var options = new AudioBoostServiceOptions { RampStepDelay = TimeSpan.FromMilliseconds(5), HealthCheckInterval = null };
        using var service = new AudioBoostService(_factory, _settings, options, TimeProvider.System, NullLogger<AudioBoostService>.Instance);
        var started = TimeProvider.System.GetTimestamp();

        await service.EnableAsync();

        // 22 steps of at most 0.5 dB up to +10.57 dB: 21 pauses.
        Assert.That(_factory.Last!.AppliedGains, Has.Count.EqualTo(22));
        Assert.That(TimeProvider.System.GetElapsedTime(started), Is.GreaterThanOrEqualTo(TimeSpan.FromMilliseconds(21 * 5)));
    }

    private AudioBoostService CreateService()
    {
        var options = new AudioBoostServiceOptions { RampStepDelay = TimeSpan.Zero, HealthCheckInterval = null };
        return new AudioBoostService(_factory, _settings, options, TimeProvider.System, NullLogger<AudioBoostService>.Instance);
    }
}
