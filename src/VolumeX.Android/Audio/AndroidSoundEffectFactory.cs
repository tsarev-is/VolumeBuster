using Android.Content;
using Microsoft.Extensions.Logging;
using VolumeX.Audio;

namespace VolumeX.Android.Audio;

/// <summary>
/// Puts DynamicsProcessing on the global output mix, falling back to LoudnessEnhancer for the volume alone.
/// </summary>
internal sealed class AndroidSoundEffectFactory : ISoundEffectFactory
{
    private readonly Context _context;
    private readonly ILogger<AndroidSoundEffectFactory> _logger;

    public AndroidSoundEffectFactory(Context context, ILogger<AndroidSoundEffectFactory> logger)
    {
        _context = context;
        _logger = logger;
    }

    public ISoundEffect Create()
    {
        var failures = new List<string>();
        var isControlledElsewhere = false;

        foreach (var (name, create) in Candidates())
        {
            ISoundEffect effect;
            try
            {
                effect = create();
            }
            catch (Java.Lang.Exception exception)
            {
                // UnsupportedOperationException: no such effect; RuntimeException: the global session was refused.
                _logger.LogWarning(exception, $"{name} on the global output is unavailable.");
                failures.Add($"{name}: {exception.Message}");
                continue;
            }

            if (effect.HasControl)
            {
                _logger.LogInformation($"{name} created on the global output (session 0).");
                return effect;
            }

            _logger.LogWarning($"{name} on the global output is controlled by another app.");
            isControlledElsewhere = true;
            effect.Dispose();
        }

        if (isControlledElsewhere)
        {
            throw new InvalidOperationException(_context.GetString(Resource.String.effect_controlled_elsewhere));
        }

        throw new EffectUnavailableException(_context.GetString(Resource.String.effect_unavailable, new Java.Lang.String(string.Join("; ", failures))));
    }

    private static IEnumerable<(string Name, Func<ISoundEffect> Create)> Candidates()
    {
        yield return ("Dynamics Processing", () => new DynamicsProcessingEffect());
        yield return ("Loudness Enhancer", () => new LoudnessEnhancerEffect());
    }
}
