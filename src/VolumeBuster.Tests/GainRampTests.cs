using VolumeBuster.Audio;

namespace VolumeBuster.Tests;

public sealed class GainRampTests
{
    [Test]
    public void Steps_RiseInSmallIncrementsAndEndExactlyOnTarget()
    {
        var steps = GainRamp.Steps(0, 10.5655, 0.5);

        Assert.That(steps, Has.Count.EqualTo(22));
        Assert.That(steps[^1], Is.EqualTo(10.5655));
        Assert.That(steps, Is.Ordered.Ascending);
        Assert.That(steps.Prepend(0).Zip(steps, (previous, next) => next - previous), Has.All.LessThanOrEqualTo(0.5 + 1e-9));
    }

    [Test]
    public void Steps_DropAtOnce()
    {
        var steps = GainRamp.Steps(10, 3, 0.5);

        Assert.That(steps, Is.EqualTo(new[] { 3.0 }));
    }

    [Test]
    public void Steps_AreEmptyWhenNothingChanges()
    {
        Assert.That(GainRamp.Steps(5, 5, 0.5), Is.Empty);
    }

    [Test]
    public void Steps_TakeOneStepForASmallIncrease()
    {
        Assert.That(GainRamp.Steps(0, 0.3, 0.5), Is.EqualTo(new[] { 0.3 }));
    }

    [TestCase(0)]
    [TestCase(-1)]
    public void Steps_RejectNonPositiveStepSize(double maxStep)
    {
        Assert.That(() => GainRamp.Steps(0, 1, maxStep), Throws.InstanceOf<ArgumentOutOfRangeException>());
    }
}
