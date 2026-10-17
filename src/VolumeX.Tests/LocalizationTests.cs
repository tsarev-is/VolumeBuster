using System.Collections;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging.Abstractions;
using VolumeX.Audio;
using VolumeX.Localization;
using VolumeX.Settings;
using VolumeX.Tests.Fakes;
using VolumeX.ViewModels;

namespace VolumeX.Tests;

/// <summary>
/// Every text exists in every interface language with the same placeholders, and the screen speaks the
/// language of the current culture.
/// </summary>
public sealed partial class LocalizationTests
{
    private static IEnumerable<string> Translations => AppLanguages.Supported.Skip(1);

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
        var options = new AudioBoostServiceOptions { RampStepDelay = TimeSpan.Zero, HealthCheckInterval = null };
        _service = new AudioBoostService(_factory, _settings, options, TimeProvider.System, NullLogger<AudioBoostService>.Instance);
    }

    [TearDown]
    public void TearDown()
    {
        _service.Dispose();
    }

    [TestCaseSource(nameof(Translations))]
    public void Translation_HasEveryTextWithTheSamePlaceholders(string language)
    {
        var english = ReadTexts(CultureInfo.InvariantCulture);
        var translated = ReadTexts(CultureInfo.GetCultureInfo(language));

        Assert.That(translated.Keys, Is.EquivalentTo(english.Keys));
        foreach (var (key, text) in english)
        {
            Assert.That(translated[key], Is.Not.Empty, key);
            Assert.That(Placeholders(translated[key]), Is.EqualTo(Placeholders(text)), key);
        }
    }

    [TestCase("en-US", "Custom")]
    [TestCase("ru-RU", "Свой")]
    [TestCase("de-DE", "Benutzerdefiniert")]
    public void PresetNames_FollowTheLanguageAndStayDistinct(string culture, string customName)
    {
        var previous = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            var names = EqualizerPresets.All.Select(preset => preset.Name).Append(EqualizerPresets.CustomName).ToList();

            Assert.That(EqualizerPresets.CustomName, Is.EqualTo(customName));
            Assert.That(names, Is.Unique);
            Assert.That(names, Has.None.Empty);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previous;
        }
    }

    [Test]
    [SetCulture("en-US")]
    [SetUICulture("en-US")]
    public void Screen_InEnglish()
    {
        using var viewModel = CreateViewModel();

        Assert.That(viewModel.GainPercentText, Is.EqualTo("125%"));
        Assert.That(viewModel.GainDecibelsText, Does.Match(@"^\+\d+\.\d\u00A0dB$"));
        Assert.That(viewModel.MediaVolumeText, Is.EqualTo("16 of 16"));
        Assert.That(viewModel.SelectedPreset.Name, Is.EqualTo("Flat"));
        Assert.That(Strings.VolumeTitle, Is.EqualTo("Volume"));
    }

    [Test]
    [SetCulture("ru-RU")]
    [SetUICulture("ru-RU")]
    public void Screen_InRussian()
    {
        using var viewModel = CreateViewModel();

        Assert.That(viewModel.GainPercentText, Is.EqualTo("125\u00A0%"));
        Assert.That(viewModel.GainDecibelsText, Does.Match(@"^\+\d+,\d\u00A0дБ$"));
        Assert.That(viewModel.MediaVolumeText, Is.EqualTo("16 из 16"));
        Assert.That(viewModel.SelectedPreset.Name, Is.EqualTo("Ровный"));
        Assert.That(Strings.VolumeTitle, Is.EqualTo("Громкость"));
    }

    [Test]
    [SetCulture("de-DE")]
    [SetUICulture("de-DE")]
    public void Screen_InGerman()
    {
        using var viewModel = CreateViewModel();

        Assert.That(viewModel.GainPercentText, Is.EqualTo("125\u00A0%"));
        Assert.That(viewModel.GainDecibelsText, Does.Match(@"^\+\d+,\d\u00A0dB$"));
        Assert.That(viewModel.MediaVolumeText, Is.EqualTo("16 von 16"));
        Assert.That(viewModel.SelectedPreset.Name, Is.EqualTo("Neutral"));
        Assert.That(Strings.VolumeTitle, Is.EqualTo("Lautstärke"));
    }

    [Test]
    [SetCulture("fr-FR")]
    [SetUICulture("fr-FR")]
    public void Screen_InAnUntranslatedLanguage_HasEnglishTextsAndLocalNumbers()
    {
        using var viewModel = CreateViewModel();

        Assert.That(viewModel.GainDecibelsText, Does.Match(@"^\+\d+,\d\u00A0dB$"));
        Assert.That(viewModel.MediaVolumeText, Is.EqualTo("16 of 16"));
        Assert.That(Strings.VolumeTitle, Is.EqualTo("Volume"));
    }

    [Test]
    [SetUICulture("de-DE")]
    public async Task EngineMessages_FollowTheLanguage()
    {
        _factory.Configure = effect => effect.BandFrequencies = [];
        await _service.EnableAsync();

        await _service.SetEqualizerEnabledAsync(true);

        Assert.That(_service.State.Tone.Message, Is.EqualTo(
            "Fake effect ändert nur die Lautstärke: Equalizer und Bass sind auf diesem Gerät nicht verfügbar."));
    }

    /// <summary>
    /// The texts of exactly one language, without falling back to English.
    /// </summary>
    private static Dictionary<string, string> ReadTexts(CultureInfo culture)
    {
        var set = Strings.ResourceManager.GetResourceSet(culture, createIfNotExists: true, tryParents: false);
        Assert.That(set, Is.Not.Null, $"No texts for {culture.Name}.");
        return set!.Cast<DictionaryEntry>().ToDictionary(entry => (string)entry.Key, entry => (string)entry.Value!);
    }

    private static List<string> Placeholders(string text)
    {
        return PlaceholderPattern().Matches(text).Select(match => match.Value).Order(StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex(@"\{\d+(:[^}]*)?\}")]
    private static partial Regex PlaceholderPattern();

    private MainViewModel CreateViewModel()
    {
        return new MainViewModel(_service, _settings, _diagnostics, new ImmediateUiDispatcher(), NullLogger<MainViewModel>.Instance);
    }
}
