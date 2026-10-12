using Android.Content;
using Android.Media;
using Android.Media.Audiofx;
using Android.OS;
using Microsoft.Extensions.Logging;
using VolumeBuster.Diagnostics;

namespace VolumeBuster.Android.Diagnostics;

/// <summary>
/// Reads the media route, media volume and effect support from <see cref="AudioManager"/>,
/// and refreshes when a device connects or the volume changes.
/// </summary>
internal sealed class AndroidAudioDiagnosticsProvider : IAudioDiagnosticsProvider, IDisposable
{
    /// <summary>
    /// Broadcast AudioService sends on every volume change; the action string is not part of the public SDK,
    /// so on a device without it the volume simply refreshes on route changes and when the app resumes.
    /// </summary>
    private const string VolumeChangedAction = "android.media.VOLUME_CHANGED_ACTION";

    private static readonly AudioDeviceType[] MediaRoutePriority =
    [
        AudioDeviceType.BluetoothA2dp,
        AudioDeviceType.BleHeadset,
        AudioDeviceType.BleSpeaker,
        AudioDeviceType.WiredHeadphones,
        AudioDeviceType.WiredHeadset,
        AudioDeviceType.UsbHeadset,
        AudioDeviceType.UsbDevice,
        AudioDeviceType.Hdmi,
        AudioDeviceType.BuiltinSpeaker,
    ];

    private readonly Context _context;
    private readonly AudioManager _audioManager;
    private readonly ILogger<AndroidAudioDiagnosticsProvider> _logger;
    private readonly DeviceCallback _deviceCallback;
    private readonly VolumeReceiver _volumeReceiver;
    /// <summary>
    /// Names of the gain-capable effects the platform reports; null when querying failed. Kept as names so that
    /// the texts around them follow a language change.
    /// </summary>
    private readonly IReadOnlyList<string>? _availableEffects;
    private AudioDiagnostics _current;

    public AndroidAudioDiagnosticsProvider(Context context, ILogger<AndroidAudioDiagnosticsProvider> logger)
    {
        _context = context;
        _logger = logger;
        _audioManager = (AudioManager?)context.GetSystemService(Context.AudioService)
            ?? throw new InvalidOperationException("AudioManager is unavailable.");
        _availableEffects = QueryAvailableEffects();
        _current = Read();

        var handler = new Handler(Looper.MainLooper!);
        _deviceCallback = new DeviceCallback(this);
        _audioManager.RegisterAudioDeviceCallback(_deviceCallback, handler);

        _volumeReceiver = new VolumeReceiver(this);
        var filter = new IntentFilter(VolumeChangedAction);
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            context.RegisterReceiver(_volumeReceiver, filter, ReceiverFlags.NotExported);
        }
        else
        {
            context.RegisterReceiver(_volumeReceiver, filter);
        }
    }

    public AudioDiagnostics Current => Volatile.Read(ref _current);

    public event EventHandler? Changed;

    public void Refresh()
    {
        try
        {
            Volatile.Write(ref _current, Read());
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Reading audio diagnostics failed.");
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _audioManager.UnregisterAudioDeviceCallback(_deviceCallback);
        _context.UnregisterReceiver(_volumeReceiver);
        _deviceCallback.Dispose();
        _volumeReceiver.Dispose();
    }

    private AudioDiagnostics Read()
    {
        var device = FindMediaDevice();
        return new AudioDiagnostics(
            $"Android {Build.VERSION.Release} (API {(int)Build.VERSION.SdkInt})",
            $"{Build.Manufacturer} {Build.Model}",
            DescribeMediaRoute(device),
            NameMediaDevice(device),
            _audioManager.GetStreamVolume(global::Android.Media.Stream.Music),
            _audioManager.GetStreamMaxVolume(global::Android.Media.Stream.Music),
            DescribeAvailableEffects());
    }

    private string DescribeMediaRoute(AudioDeviceInfo? device)
    {
        if (device is null)
        {
            return "—";
        }

        var kind = Kind(device);
        var name = ProductName(device);
        return name is null ? kind : $"{kind} · {name}";
    }

    private string NameMediaDevice(AudioDeviceInfo? device)
    {
        return device is null ? "—" : ProductName(device) ?? Kind(device);
    }

    /// <summary>
    /// The product name of an external device; null for the phone's own speakers and nameless devices.
    /// </summary>
    private static string? ProductName(AudioDeviceInfo device)
    {
        var name = device.ProductNameFormatted?.ToString();
        var isBuiltIn = device.Type is AudioDeviceType.BuiltinSpeaker or AudioDeviceType.BuiltinSpeakerSafe or AudioDeviceType.BuiltinEarpiece;
        return isBuiltIn || string.IsNullOrWhiteSpace(name) || name == Build.Model ? null : name;
    }

    private string Kind(AudioDeviceInfo device)
    {
        return device.Type switch
        {
            AudioDeviceType.BuiltinSpeaker or AudioDeviceType.BuiltinSpeakerSafe => _context.GetString(Resource.String.route_phone_speaker),
            AudioDeviceType.BuiltinEarpiece => _context.GetString(Resource.String.route_earpiece),
            AudioDeviceType.BluetoothA2dp => "Bluetooth A2DP",
            AudioDeviceType.BluetoothSco => _context.GetString(Resource.String.route_bluetooth_sco),
            AudioDeviceType.BleHeadset or AudioDeviceType.BleSpeaker => "Bluetooth LE Audio",
            AudioDeviceType.WiredHeadphones or AudioDeviceType.WiredHeadset => _context.GetString(Resource.String.route_wired_headphones),
            AudioDeviceType.UsbHeadset or AudioDeviceType.UsbDevice or AudioDeviceType.UsbAccessory => _context.GetString(Resource.String.route_usb),
            AudioDeviceType.Hdmi or AudioDeviceType.HdmiArc or AudioDeviceType.HdmiEarc => "HDMI",
            AudioDeviceType.RemoteSubmix => _context.GetString(Resource.String.route_remote_submix),
            _ => device.Type.ToString(),
        };
    }

    private AudioDeviceInfo? FindMediaDevice()
    {
        if (OperatingSystem.IsAndroidVersionAtLeast(33))
        {
            var attributes = new AudioAttributes.Builder().SetUsage(AudioUsageKind.Media)!.Build()!;
            var routed = _audioManager.GetAudioDevicesForAttributes(attributes);
            if (routed.Count > 0)
            {
                return routed[0];
            }
        }

        // Before Android 13 there is no public "where does media go" query: take the device policy would prefer.
        var outputs = _audioManager.GetDevices(GetDevicesTargets.Outputs) ?? [];
        return MediaRoutePriority
            .Select(type => outputs.FirstOrDefault(device => device.Type == type))
            .FirstOrDefault(device => device is not null);
    }

    private string DescribeAvailableEffects()
    {
        return _availableEffects switch
        {
            null => _context.GetString(Resource.String.effects_unknown),
            [] => _context.GetString(Resource.String.effects_none),
            var names => string.Join(", ", names),
        };
    }

    private IReadOnlyList<string>? QueryAvailableEffects()
    {
        try
        {
            var descriptors = AudioEffect.QueryEffects() ?? [];
            var names = new List<string>();
            if (descriptors.Any(descriptor => descriptor.Type?.Equals(AudioEffect.EffectTypeDynamicsProcessing) == true))
            {
                names.Add("Dynamics Processing");
            }

            if (descriptors.Any(descriptor => descriptor.Type?.Equals(AudioEffect.EffectTypeLoudnessEnhancer) == true))
            {
                names.Add("Loudness Enhancer");
            }

            return names;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Querying audio effects failed.");
            return null;
        }
    }

    /// <summary>
    /// Refreshes when an output device appears or disappears.
    /// </summary>
    private sealed class DeviceCallback : AudioDeviceCallback
    {
        private readonly AndroidAudioDiagnosticsProvider _owner;

        public DeviceCallback(AndroidAudioDiagnosticsProvider owner)
        {
            _owner = owner;
        }

        public override void OnAudioDevicesAdded(AudioDeviceInfo[]? addedDevices)
        {
            _owner.Refresh();
        }

        public override void OnAudioDevicesRemoved(AudioDeviceInfo[]? removedDevices)
        {
            _owner.Refresh();
        }
    }

    /// <summary>
    /// Refreshes when any stream volume changes.
    /// </summary>
    private sealed class VolumeReceiver : BroadcastReceiver
    {
        private readonly AndroidAudioDiagnosticsProvider _owner;

        public VolumeReceiver(AndroidAudioDiagnosticsProvider owner)
        {
            _owner = owner;
        }

        public override void OnReceive(Context? context, Intent? intent)
        {
            _owner.Refresh();
        }
    }
}
