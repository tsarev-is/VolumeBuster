using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using VolumeX.Localization;

namespace VolumeX.Tests;

/// <summary>
/// The Android project's own texts (tile, notification, diagnostics) exist in every interface language with the
/// same placeholders; the .NET build does not run Android lint, which would otherwise catch a missing one.
/// </summary>
public sealed partial class AndroidStringsTests
{
    private static IEnumerable<string> Translations => AppLanguages.Supported.Skip(1);

    [TestCaseSource(nameof(Translations))]
    public void Translation_HasEveryTextWithTheSamePlaceholders(string language)
    {
        var english = ReadStrings("values");
        var translated = ReadStrings($"values-{language}");

        Assert.That(translated.Keys, Is.EquivalentTo(english.Keys));
        foreach (var (name, text) in english)
        {
            Assert.That(translated[name], Is.Not.Empty, name);
            Assert.That(Placeholders(translated[name]), Is.EqualTo(Placeholders(text)), name);
        }
    }

    [Test]
    public void LocaleConfig_OffersEveryInterfaceLanguage()
    {
        var path = Path.Combine(AndroidResources(), "xml", "locales_config.xml");
        var android = XNamespace.Get("http://schemas.android.com/apk/res/android");
        var locales = XDocument.Load(path).Root!.Elements("locale").Select(locale => (string?)locale.Attribute(android + "name"));

        Assert.That(locales, Is.EquivalentTo(AppLanguages.Supported));
    }

    private static Dictionary<string, string> ReadStrings(string folder)
    {
        var path = Path.Combine(AndroidResources(), folder, "strings.xml");
        return XDocument.Load(path).Root!.Elements("string")
            .ToDictionary(element => (string)element.Attribute("name")!, element => element.Value);
    }

    private static List<string> Placeholders(string text)
    {
        return PlaceholderPattern().Matches(text).Select(match => match.Value).Order(StringComparer.Ordinal).ToList();
    }

    [GeneratedRegex(@"%(\d+\$)?[a-z%]")]
    private static partial Regex PlaceholderPattern();

    private static string AndroidResources([CallerFilePath] string testFile = "")
    {
        return Path.Combine(Path.GetDirectoryName(testFile)!, "..", "VolumeX.Android", "Resources");
    }
}
