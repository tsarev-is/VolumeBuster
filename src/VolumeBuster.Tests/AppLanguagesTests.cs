using VolumeBuster.Localization;

namespace VolumeBuster.Tests;

public sealed class AppLanguagesTests
{
    [TestCase(new[] { "de-AT" }, "de-AT")]
    [TestCase(new[] { "ru-RU", "de-DE" }, "ru-RU")]
    [TestCase(new[] { "en-GB" }, "en-GB")]
    public void Resolve_TakesTheFirstLanguageAsItIs(string[] languageTags, string expected)
    {
        Assert.That(AppLanguages.Resolve(languageTags).Name, Is.EqualTo(expected));
    }

    [TestCase(new[] { "fr-FR", "de-DE" }, "de-DE")]
    [TestCase(new[] { "uk-UA", "ru-RU", "en-US" }, "ru-RU")]
    public void Resolve_SkipsLanguagesWithoutATranslation(string[] languageTags, string expected)
    {
        // As Android does for the app's resources: values-de is picked over the missing values-fr.
        Assert.That(AppLanguages.Resolve(languageTags).Name, Is.EqualTo(expected));
    }

    [Test]
    public void Resolve_KeepsTheFirstLanguageWhenNoneIsTranslated()
    {
        // The numbers stay French; the texts fall back to English on their own.
        Assert.That(AppLanguages.Resolve(["fr-FR", "es-ES"]).Name, Is.EqualTo("fr-FR"));
    }

    [TestCase("en-US-u-mu-celsius", "en-US")]
    [TestCase("de-DE-u-fw-mon", "de-DE")]
    [TestCase("ru_RU", "ru-RU")]
    [TestCase("ru-XX", "ru")]
    public void Resolve_ReadsTagsWithExtensionsAndUnknownParts(string languageTag, string expected)
    {
        // Android 14+ adds regional preferences as -u- extensions.
        Assert.That(AppLanguages.Resolve([languageTag]).Name, Is.EqualTo(expected));
    }

    [Test]
    public void Resolve_FallsBackToEnglish()
    {
        Assert.That(AppLanguages.Resolve([]).Name, Is.EqualTo("en"));
        Assert.That(AppLanguages.Resolve(["und", ""]).Name, Is.EqualTo("en"));
    }

    [Test]
    public void Resolve_IgnoresMissingTags()
    {
        Assert.That(AppLanguages.Resolve([null, "de-CH"]).Name, Is.EqualTo("de-CH"));
    }
}
