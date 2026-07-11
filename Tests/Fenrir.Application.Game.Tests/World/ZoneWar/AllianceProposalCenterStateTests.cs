using Fenrir.Application.Game.Domain.World.ZoneWar;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class AllianceProposalCenterStateTests
{
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, true)]
    [InlineData(-1, false)]
    [InlineData(2, false)]
    public void IsValidSlot_MatchesTheTwoSlotArray(int slot, bool expected)
    {
        Assert.Equal(expected, AllianceProposalCenterState.IsValidSlot(slot));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(3, true)]
    [InlineData(-1, false)]
    [InlineData(4, false)]
    public void IsValidTribe_MatchesTheFourTribeArray(int tribeId, bool expected)
    {
        Assert.Equal(expected, AllianceProposalCenterState.IsValidTribe(tribeId));
    }

    [Fact]
    public void NewState_BothSlotsStartEmpty()
    {
        var state = new AllianceProposalCenterState();

        Assert.True(state.SlotIsEmpty(0));
        Assert.True(state.SlotIsEmpty(1));
        Assert.Equal((null, null), state.GetSlot(0));
    }

    [Fact]
    public void SetSlot_ThenGet_RoundTrips_LeavesTheOtherSlotUntouched()
    {
        var state = new AllianceProposalCenterState();

        state.SetSlot(0, 1, 2);

        Assert.Equal(((byte?)1, (byte?)2), state.GetSlot(0));
        Assert.True(state.SlotIsEmpty(1));
    }

    [Fact]
    public void ClearSlot_ReturnsBothCellsToEmpty()
    {
        var state = new AllianceProposalCenterState();
        state.SetSlot(1, 2, 3);

        state.ClearSlot(1);

        Assert.True(state.SlotIsEmpty(1));
    }

    [Theory]
    [InlineData(1, 2, 1, 2, true)]
    [InlineData(1, 2, 2, 1, true)] // either order
    [InlineData(1, 2, 1, 3, false)]
    [InlineData(1, 2, 3, 4, false)]
    public void SlotMatchesPairEitherOrder_ChecksBothOrderings(byte cellA, byte cellB, byte queryA, byte queryB,
        bool expected)
    {
        var state = new AllianceProposalCenterState();
        state.SetSlot(0, cellA, cellB);

        Assert.Equal(expected, state.SlotMatchesPairEitherOrder(0, queryA, queryB));
    }

    [Fact]
    public void SlotMatchesPairEitherOrder_EmptySlot_NeverMatches()
    {
        var state = new AllianceProposalCenterState();

        Assert.False(state.SlotMatchesPairEitherOrder(0, 1, 2));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void SetSlot_OutOfRangeSlot_Throws(int slot)
    {
        var state = new AllianceProposalCenterState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.SetSlot(slot, 0, 1));
    }

    [Fact]
    public void GetPossibleAllianceInfo_DefaultsToCleared()
    {
        var state = new AllianceProposalCenterState();

        var info = state.GetPossibleAllianceInfo(2);

        Assert.Equal(AlliancePossibleInfo.Cleared, info);
        Assert.False(info.CooldownActive);
        Assert.Equal(0, info.ExpiryDateYyyyMmDd);
    }

    [Fact]
    public void SetPossibleAllianceCooldown_ArmsTheFlagAndStoresTheExpiryDate()
    {
        var state = new AllianceProposalCenterState();

        state.SetPossibleAllianceCooldown(1, 20260815);

        var info = state.GetPossibleAllianceInfo(1);
        Assert.True(info.CooldownActive);
        Assert.Equal(20260815, info.ExpiryDateYyyyMmDd);

        // Untouched tribe stays cleared.
        Assert.Equal(AlliancePossibleInfo.Cleared, state.GetPossibleAllianceInfo(0));
    }

    [Fact]
    public void ClearPossibleAllianceCooldown_ZeroesBothCells()
    {
        var state = new AllianceProposalCenterState();
        state.SetPossibleAllianceCooldown(3, 20260101);

        state.ClearPossibleAllianceCooldown(3);

        Assert.Equal(AlliancePossibleInfo.Cleared, state.GetPossibleAllianceInfo(3));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(200)]
    public void GetPossibleAllianceInfo_OutOfRangeTribe_Throws(byte tribeId)
    {
        var state = new AllianceProposalCenterState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.GetPossibleAllianceInfo(tribeId));
    }

    [Fact]
    public void ApplyFinalize_SlotZeroEmpty_SelectsSlotZero_AsOneAtomicWrite()
    {
        var state = new AllianceProposalCenterState();

        state.ApplyFinalize(0, 1);

        Assert.Equal(((byte?)0, (byte?)1), state.GetSlot(0));
        Assert.True(state.SlotIsEmpty(1));
        Assert.Equal(AlliancePossibleInfo.Cleared, state.GetPossibleAllianceInfo(0));
        Assert.Equal(AlliancePossibleInfo.Cleared, state.GetPossibleAllianceInfo(1));
    }

    [Fact]
    public void ApplyFinalize_SlotZeroOccupied_FallsBackToSlotOne_NoIndependentVerification()
    {
        var state = new AllianceProposalCenterState();
        state.SetSlot(0, 2, 3);
        state.SetSlot(1, 0, 1); // pre-existing occupant, overwritten unconditionally

        state.ApplyFinalize(0, 1);

        Assert.Equal(((byte?)2, (byte?)3), state.GetSlot(0));
        Assert.Equal(((byte?)0, (byte?)1), state.GetSlot(1));
    }

    [Fact]
    public void ApplyFinalize_ClearsBothTribesCooldownsRegardlessOfPriorValue()
    {
        var state = new AllianceProposalCenterState();
        state.SetPossibleAllianceCooldown(0, 20260101);
        state.SetPossibleAllianceCooldown(1, 20260202);

        state.ApplyFinalize(0, 1);

        Assert.Equal(AlliancePossibleInfo.Cleared, state.GetPossibleAllianceInfo(0));
        Assert.Equal(AlliancePossibleInfo.Cleared, state.GetPossibleAllianceInfo(1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void ApplyFinalize_OutOfRangeTribe_Throws(int badTribe)
    {
        var state = new AllianceProposalCenterState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.ApplyFinalize((byte)badTribe, 1));
    }

    [Fact]
    public void ApplyBreak_SlotZeroMatchesPairEitherOrder_SelectsSlotZero_ArmsCooldowns_ClearsSlot()
    {
        var state = new AllianceProposalCenterState();
        state.SetSlot(0, 1, 2);

        state.ApplyBreak(2, 1, 20260815, 20260816); // opposite order from how the slot was stored

        Assert.True(state.SlotIsEmpty(0));
        var infoTribe2 = state.GetPossibleAllianceInfo(2);
        Assert.True(infoTribe2.CooldownActive);
        Assert.Equal(20260815, infoTribe2.ExpiryDateYyyyMmDd);
        var infoTribe1 = state.GetPossibleAllianceInfo(1);
        Assert.True(infoTribe1.CooldownActive);
        Assert.Equal(20260816, infoTribe1.ExpiryDateYyyyMmDd);
    }

    [Fact]
    public void ApplyBreak_SlotZeroDoesNotMatch_FallsBackToSlotOne_UnconditionallyClearingIt()
    {
        var state = new AllianceProposalCenterState();
        state.SetSlot(0, 3, 0); // unrelated pair
        state.SetSlot(1, 1, 2); // the actual targeted pair

        state.ApplyBreak(1, 2, 20260901, 20260902);

        Assert.Equal(((byte?)3, (byte?)0), state.GetSlot(0));
        Assert.True(state.SlotIsEmpty(1)); // cleared even though it genuinely held the pair -- legacy defect preserved
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(4)]
    public void ApplyBreak_OutOfRangeTribe_Throws(int badTribe)
    {
        var state = new AllianceProposalCenterState();

        Assert.Throws<ArgumentOutOfRangeException>(() => state.ApplyBreak((byte)badTribe, 1, 1, 1));
    }
}
