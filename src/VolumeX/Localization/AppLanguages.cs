using System.Globalization;

namespace VolumeX.Localization;

/// <summary>
/// The interface languages and the choice among them by the user's system languages.
/// </summary>
/// <remarks>
/// The choice mirrors how Android picks the app's own resources (<c>values-ru</c>, <c>values-de</c>): the first
/// system language with a translation wins, so the screen, the notification and the tile always agree.
/// </remarks>
public static class AppLanguages
{
    /// <summary>
    /// Languages with a translation; the first one, English, is what other languages fall back to.
    /// </summary>
    public static IReadOnlyList<string> Supported { get; } = ["en", "ru", "de"];

    /// <summary>
    /// The culture for the user's languages, most preferred first, as BCP 47 tags ("de-AT", "en-US-u-mu-celsius").
    /// </summary>
    /// <remarks>
    /// The first language with a translation wins. Without one, the first language is kept for numbers while
    /// the texts fall back to English; without any usable language, English is used throughout.
    /// </remarks>
    public static CultureInfo Resolve(IEnumerable<string?> languageTags)
    {
        var cultures = languageTags.Select(Find).OfType<CultureInfo>().ToList();
        return cultures.FirstOrDefault(culture => Supported.Contains(culture.TwoLetterISOLanguageName))
            ?? cultures.FirstOrDefault()
            ?? CultureInfo.GetCultureInfo(Supported[0]);
    }

    /// <summary>
    /// Makes <paramref name="culture"/> the language of the texts and the numbers on the calling thread and on
    /// every thread without a culture of its own.
    /// </summary>
    /// <remarks>
    /// Call it on the main thread. The defaults alone are not enough: on Android the main thread already has a
    /// culture of its own, which hides them. Async operations already running keep the culture they started with.
    /// </remarks>
    public static void Apply(CultureInfo culture)
    {
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
    }

    /// <summary>
    /// A known culture for <paramref name="languageTag"/>, dropping extensions and then trailing subtags
    /// ("sr-Latn-XK" → "sr-Latn" → "sr") until one is known; null when none is.
    /// </summary>
    private static CultureInfo? Find(string? languageTag)
    {
        // Subtags after a single-letter one are extensions (-u-, -x-), which no culture name carries.
        var subtags = (languageTag ?? string.Empty)
            .Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)
            .TakeWhile(subtag => subtag.Length > 1)
            .ToList();

        for (var count = subtags.Count; count > 0; count--)
        {
            try
            {
                var culture = CultureInfo.GetCultureInfo(string.Join('-', subtags.Take(count)), predefinedOnly: true);

                // "und" (undetermined) comes back as the invariant culture, which is nobody's language.
                return culture.Name.Length > 0 ? culture : null;
            }
            catch (CultureNotFoundException)
            {
            }
        }

        return null;
    }
}
