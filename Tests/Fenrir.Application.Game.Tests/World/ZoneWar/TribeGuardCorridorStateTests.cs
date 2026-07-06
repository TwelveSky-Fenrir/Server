using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

/// <summary>
///     Covers <see cref="TribeGuardCorridorState" /> in isolation: the cluster-boot-closed default, transition
///     bookkeeping, and range validation for the 4x4 <c>mTribeGuardState</c> table.
/// </summary>
public class TribeGuardCorridorStateTests
{
    [Fact]
    public void FreshInstance_EverySegmentOfEveryTribeStartsClosed()
    {
        var state = new TribeGuardCorridorState();

        for (byte tribeId = 0; tribeId < TribeGuardCorridorState.TribeCount; tribeId++)
        for (byte segmentIndex = 0; segmentIndex < TribeGuardCorridorState.SegmentCount; segmentIndex++)
            Assert.False(state.IsOpen(tribeId, segmentIndex));
    }

    [Fact]
    public void TrySetOpen_ReturnsTrueAndAppliesOnAnActualTransition()
    {
        var state = new TribeGuardCorridorState();

        Assert.True(state.TrySetOpen(0, 0, true));
        Assert.True(state.IsOpen(0, 0));
    }

    [Fact]
    public void TrySetOpen_ReturnsFalseAndLeavesStateUnchanged_WhenAlreadyAtTheRequestedValue()
    {
        var state = new TribeGuardCorridorState();

        // Already closed -- requesting closed again must be a documented no-op (no redundant notification).
        Assert.False(state.TrySetOpen(0, 0, false));
        Assert.False(state.IsOpen(0, 0));

        state.TrySetOpen(0, 0, true);
        Assert.False(state.TrySetOpen(0, 0, true)); // already open -- requesting open again is a no-op
        Assert.True(state.IsOpen(0, 0));
    }

    [Fact]
    public void EachTribeAndSegmentIsIndependent()
    {
        var state = new TribeGuardCorridorState();

        state.TrySetOpen(1, 2, true);

        Assert.True(state.IsOpen(1, 2));
        Assert.False(state.IsOpen(1, 1));
        Assert.False(state.IsOpen(0, 2));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(255)]
    public void IsOpen_RejectsAnOutOfRangeTribeId(byte tribeId)
    {
        var state = new TribeGuardCorridorState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.IsOpen(tribeId, 0));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(255)]
    public void IsOpen_RejectsAnOutOfRangeSegmentIndex(byte segmentIndex)
    {
        var state = new TribeGuardCorridorState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.IsOpen(0, segmentIndex));
    }
}
