using VolumeBuster.Audio;

namespace VolumeBuster.Tests.Fakes;

/// <summary>
/// Hands out <see cref="FakeSoundEffect"/> instances or throws a configured exception.
/// </summary>
internal sealed class FakeSoundEffectFactory : ISoundEffectFactory
{
    public List<FakeSoundEffect> Created { get; } = [];

    public FakeSoundEffect? Last => Created.Count > 0 ? Created[^1] : null;

    /// <summary>
    /// Thrown by <see cref="Create"/> instead of creating an effect.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Applied to every new effect before it is returned.
    /// </summary>
    public Action<FakeSoundEffect>? Configure { get; set; }

    public ISoundEffect Create()
    {
        if (Exception is not null)
        {
            throw Exception;
        }

        var effect = new FakeSoundEffect();
        Configure?.Invoke(effect);
        Created.Add(effect);
        return effect;
    }
}
