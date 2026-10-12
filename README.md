# VolumeBuster

Android volume booster above 100% for all apps, no root. Boost up to +10 dB, bass and equalizer.

<p align="center">
  <img src="docs/screenshots/main-light.png" width="270" alt="VolumeBuster main screen, light theme">
  <img src="docs/screenshots/main-dark.png" width="270" alt="VolumeBuster main screen, dark theme">
</p>

## Features

- Boost from 100% to 150% on top of the system maximum.
- Bass from −9 to +9 dB and an equalizer, presets and a custom curve.
- Works in the background.
- Light and dark theme.
- English, Russian and German.

Requires Android 12 or newer. Some devices do not allow effects on the global audio output;
the diagnostics section of the app shows what the device offers.

## Privacy

No network permission, no ads, no analytics: nothing leaves the device. See the [privacy policy](PRIVACY.md).

## Build

Requires .NET SDK 10 with the `android` workload, Android SDK 36 and JDK 21.

```bash
cd src
dotnet test VolumeBuster.Tests
dotnet publish VolumeBuster.Android -c Release
```

Changes are listed in [CHANGELOG.md](CHANGELOG.md).

## License

[MIT](LICENSE)
