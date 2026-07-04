using Fenrir.Application.Game.Simulation;

namespace Fenrir.Application.Game.Tests.Simulation;

/// <summary>Covers the 20 Hz network frame -> 2 Hz legacy tick bridge: the remainder must survive across calls with zero drift.</summary>
public class SimulationTickAccumulatorTests
{
    [Fact]
    public void Advance_LessThanOneLegacyTick_ReturnsZero()
    {
        var accumulator = new SimulationTickAccumulator();

        Assert.Equal(0, accumulator.Advance(TimeSpan.FromMilliseconds(499)));
    }

    [Fact]
    public void Advance_ExactlyOneLegacyTick_ReturnsOne()
    {
        var accumulator = new SimulationTickAccumulator();

        Assert.Equal(1, accumulator.Advance(SimulationClock.LegacyTick));
    }

    [Fact]
    public void Advance_SeveralLegacyTicksInOneFrame_ReturnsAllOfThemAtOnce()
    {
        // A stalled host (GC pause, debugger) must deliver the missed ticks as one burst, not one per call.
        var accumulator = new SimulationTickAccumulator();

        Assert.Equal(3, accumulator.Advance(TimeSpan.FromMilliseconds(1500)));
    }

    [Fact]
    public void Advance_CarriesRemainderAcrossCalls_UntilAWholeTickIsDue()
    {
        var accumulator = new SimulationTickAccumulator();

        // 10 frames of 50 ms must add up to exactly 1 legacy tick on the 10th, never earlier
        for (var frame = 1; frame < 10; frame++)
            Assert.Equal(0, accumulator.Advance(TimeSpan.FromMilliseconds(50)));

        Assert.Equal(1, accumulator.Advance(TimeSpan.FromMilliseconds(50)));
    }

    [Fact]
    public void Advance_ManyIrregularFrames_NeverDriftsFromWallClockRate()
    {
        var accumulator = new SimulationTickAccumulator();
        var frameMs = new[] { 10, 33, 47, 91, 12, 500, 501, 3, 499, 1000 };

        var totalElapsed = TimeSpan.Zero;
        var totalTicks = 0;

        foreach (var ms in frameMs)
        {
            var frame = TimeSpan.FromMilliseconds(ms);
            totalElapsed += frame;
            totalTicks += accumulator.Advance(frame);
        }

        var expectedTicks = (int)(totalElapsed.Ticks / SimulationClock.LegacyTick.Ticks);
        Assert.Equal(expectedTicks, totalTicks);
    }

    [Fact]
    public void Advance_ZeroOrNegativeElapsed_ContributesNothing_AndDoesNotRewindTheAccumulator()
    {
        var accumulator = new SimulationTickAccumulator();

        Assert.Equal(0, accumulator.Advance(TimeSpan.FromMilliseconds(400)));

        // a zero/negative-elapsed hiccup must not discard or rewind the 400 ms already banked
        Assert.Equal(0, accumulator.Advance(TimeSpan.Zero));
        Assert.Equal(0, accumulator.Advance(TimeSpan.FromMilliseconds(-100)));

        Assert.Equal(1, accumulator.Advance(TimeSpan.FromMilliseconds(100)));
    }
}
