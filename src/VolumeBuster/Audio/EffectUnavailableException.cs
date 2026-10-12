namespace VolumeBuster.Audio;

/// <summary>
/// The device does not allow this effect on the global media output.
/// </summary>
public sealed class EffectUnavailableException : Exception
{
    public EffectUnavailableException(string message)
        : base(message)
    {
    }

    public EffectUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
