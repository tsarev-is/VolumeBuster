using System.Globalization;
using Android.Content;
using VolumeBuster.Settings;

namespace VolumeBuster.Android.Settings;

/// <summary>
/// <see cref="ISettingsStore"/> on app-private SharedPreferences.
/// </summary>
internal sealed class SharedPreferencesSettingsStore : ISettingsStore
{
    private const string FileName = "settings";
    private const string GainPercentKey = "gain_percent";
    private const string IsEnabledKey = "is_enabled";
    private const string IsWarningAcknowledgedKey = "is_warning_acknowledged";
    private const string IsEqualizerEnabledKey = "is_equalizer_enabled";
    private const string EqualizerPresetKey = "equalizer_preset";
    private const string IsBassEnabledKey = "is_bass_enabled";
    private const string BassDecibelsKey = "bass_decibels";
    private const string CustomEqualizerGainsKey = "equalizer_custom_gains";
    private const string EqualizerBandCountKey = "equalizer_band_count";

    private readonly ISharedPreferences _preferences;
    private readonly Lock _lock = new();

    public SharedPreferencesSettingsStore(Context context)
    {
        _preferences = context.GetSharedPreferences(FileName, FileCreationMode.Private)
            ?? throw new InvalidOperationException("SharedPreferences are unavailable.");
    }

    public ApplicationSettings Load()
    {
        lock (_lock)
        {
            return Read();
        }
    }

    public void Update(Func<ApplicationSettings, ApplicationSettings> change)
    {
        lock (_lock)
        {
            var settings = change(Read());
            using var editor = _preferences.Edit() ?? throw new InvalidOperationException("SharedPreferences editor is unavailable.");
            editor.PutFloat(GainPercentKey, (float)settings.GainPercent);
            editor.PutBoolean(IsEnabledKey, settings.IsEnabled);
            editor.PutBoolean(IsWarningAcknowledgedKey, settings.IsWarningAcknowledged);
            editor.PutBoolean(IsEqualizerEnabledKey, settings.IsEqualizerEnabled);
            editor.PutString(EqualizerPresetKey, settings.EqualizerPresetId);
            editor.PutBoolean(IsBassEnabledKey, settings.IsBassEnabled);
            editor.PutFloat(BassDecibelsKey, (float)settings.BassDecibels);
            editor.PutString(CustomEqualizerGainsKey, settings.CustomEqualizerGainsDecibels is { } gains
                ? string.Join(";", gains.Select(gain => gain.ToString(CultureInfo.InvariantCulture)))
                : null);
            editor.PutInt(EqualizerBandCountKey, settings.EqualizerBandCount);
            editor.Apply();
        }
    }

    private ApplicationSettings Read()
    {
        var defaults = new ApplicationSettings();
        return new ApplicationSettings(
            _preferences.GetFloat(GainPercentKey, (float)defaults.GainPercent),
            _preferences.GetBoolean(IsEnabledKey, defaults.IsEnabled),
            _preferences.GetBoolean(IsWarningAcknowledgedKey, defaults.IsWarningAcknowledged),
            _preferences.GetBoolean(IsEqualizerEnabledKey, defaults.IsEqualizerEnabled),
            _preferences.GetString(EqualizerPresetKey, defaults.EqualizerPresetId) ?? defaults.EqualizerPresetId,
            _preferences.GetBoolean(IsBassEnabledKey, defaults.IsBassEnabled),
            _preferences.GetFloat(BassDecibelsKey, (float)defaults.BassDecibels),
            ParseGains(_preferences.GetString(CustomEqualizerGainsKey, null)),
            _preferences.GetInt(EqualizerBandCountKey, defaults.EqualizerBandCount));
    }

    private static IReadOnlyList<double>? ParseGains(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return null;
        }

        var gains = new List<double>();
        foreach (var part in text.Split(';'))
        {
            if (!double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var gain))
            {
                return null;
            }

            gains.Add(gain);
        }

        return gains;
    }
}
