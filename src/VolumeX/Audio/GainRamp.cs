namespace VolumeX.Audio;

/// <summary>
/// Splits a gain change into small steps so the sound never jumps up abruptly.
/// </summary>
public static class GainRamp
{
    /// <summary>
    /// Intermediate gains from <paramref name="fromDecibels"/> (exclusive) to <paramref name="toDecibels"/> (inclusive).
    /// </summary>
    /// <remarks>
    /// Increases rise by at most <paramref name="maxStepDecibels"/> per step; decreases happen in one step,
    /// because making the sound quieter quickly is always safe.
    /// </remarks>
    public static IReadOnlyList<double> Steps(double fromDecibels, double toDecibels, double maxStepDecibels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxStepDecibels);

        if (toDecibels.Equals(fromDecibels))
        {
            return [];
        }

        if (toDecibels < fromDecibels)
        {
            return [toDecibels];
        }

        var count = (int)Math.Ceiling((toDecibels - fromDecibels) / maxStepDecibels);
        var steps = new double[count];
        for (var i = 1; i < count; i++)
        {
            steps[i - 1] = fromDecibels + (toDecibels - fromDecibels) * i / count;
        }

        steps[count - 1] = toDecibels;
        return steps;
    }
}
