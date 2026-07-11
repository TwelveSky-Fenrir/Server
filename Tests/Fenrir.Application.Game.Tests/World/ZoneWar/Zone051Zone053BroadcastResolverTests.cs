using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class Zone051Zone053BroadcastResolverTests
{
    private static byte[] SlotPayload(int slot)
    {
        var data = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(data, slot);
        return data;
    }

    [Theory]
    [InlineData(11, 1)]
    [InlineData(14, 5)]
    [InlineData(18, 0)]
    public void ApplyZone051_WritesTheMappedState_NotTheRawSelector(int selector, int expectedState)
    {
        var state = new Zone051Zone053SiegeState();
        state.SetZone051(2, 9);

        Zone051Zone053BroadcastResolver.ApplyZone051(state, selector, SlotPayload(2), NullLogger.Instance);

        Assert.Equal(expectedState, state.GetZone051(2));
    }

    [Fact]
    public void ApplyZone051_Selector10_LeavesTheSlotUntouched()
    {
        var state = new Zone051Zone053SiegeState();
        state.SetZone051(1, 4);

        Zone051Zone053BroadcastResolver.ApplyZone051(state, 10, SlotPayload(1), NullLogger.Instance);

        Assert.Equal(4, state.GetZone051(1));
    }

    [Fact]
    public void ApplyZone051_OutOfRangeSlot_DoesNotThrow_AndWritesNothing()
    {
        var state = new Zone051Zone053SiegeState();

        Zone051Zone053BroadcastResolver.ApplyZone051(state, 11, SlotPayload(Zone051Zone053SiegeState.Zone051Slots + 3),
            NullLogger.Instance);

        for (var slot = 0; slot < Zone051Zone053SiegeState.Zone051Slots; slot++)
            Assert.Equal(0, state.GetZone051(slot));
    }

    [Theory]
    [InlineData(20, 1)]
    [InlineData(23, 5)]
    [InlineData(30, 0)]
    public void ApplyZone053_WritesTheMappedState_NotTheRawSelector(int selector, int expectedState)
    {
        var state = new Zone051Zone053SiegeState();
        state.SetZone053(5, 9);

        Zone051Zone053BroadcastResolver.ApplyZone053(state, selector, SlotPayload(5), NullLogger.Instance);

        Assert.Equal(expectedState, state.GetZone053(5));
    }

    [Fact]
    public void ApplyZone053_Selector19_IsDeadCodeInEveryShippedBuild_LeavesTheSlotUntouched()
    {
        var state = new Zone051Zone053SiegeState();
        state.SetZone053(3, 4);

        Zone051Zone053BroadcastResolver.ApplyZone053(state, 19, SlotPayload(3), NullLogger.Instance);

        Assert.Equal(4, state.GetZone053(3));
    }

    [Theory]
    [InlineData(25)]
    [InlineData(26)]
    [InlineData(27)]
    public void ApplyZone053_BareNoOpSelectors_LeaveTheSlotUntouched(int selector)
    {
        var state = new Zone051Zone053SiegeState();
        state.SetZone053(0, 2);

        Zone051Zone053BroadcastResolver.ApplyZone053(state, selector, SlotPayload(0), NullLogger.Instance);

        Assert.Equal(2, state.GetZone053(0));
    }

    [Fact]
    public void ApplyZone053_OutOfRangeSlot_DoesNotThrow_AndWritesNothing()
    {
        var state = new Zone051Zone053SiegeState();

        Zone051Zone053BroadcastResolver.ApplyZone053(state, 20, SlotPayload(Zone051Zone053SiegeState.Zone053Slots + 3),
            NullLogger.Instance);

        for (var slot = 0; slot < Zone051Zone053SiegeState.Zone053Slots; slot++)
            Assert.Equal(0, state.GetZone053(slot));
    }
}
