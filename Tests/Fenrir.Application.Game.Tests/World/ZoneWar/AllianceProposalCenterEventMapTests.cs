using System.Buffers.Binary;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.ZoneWar;

public class AllianceProposalCenterEventMapTests
{
    private static readonly NullLogger Logger = NullLogger.Instance;

    private static byte[] Payload(params int[] int32Fields)
    {
        var data = new byte[ZoneCenterBroadcastIngestor.PayloadSize];
        for (var i = 0; i < int32Fields.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(i * 4), int32Fields[i]);

        return data;
    }

    [Fact]
    public void Apply_Finalize_SlotZeroEmpty_SelectsSlotZero_ClearsCooldowns_WritesPair()
    {
        var state = new AllianceProposalCenterState();
        state.SetPossibleAllianceCooldown(0, 20260101);
        state.SetPossibleAllianceCooldown(1, 20260102);

        AllianceProposalCenterEventMap.Apply(AllianceProposalCenterEventMap.FinalizeNewAllianceEventCode,
            Payload(0, 1), state, Logger);

        Assert.Equal(((byte?)0, (byte?)1), state.GetSlot(0));
        Assert.True(state.SlotIsEmpty(1));
        Assert.Equal(AlliancePossibleInfo.Cleared, state.GetPossibleAllianceInfo(0));
        Assert.Equal(AlliancePossibleInfo.Cleared, state.GetPossibleAllianceInfo(1));
    }

    [Fact]
    public void Apply_Finalize_SlotZeroOccupied_FallsBackToSlotOne_UnconditionallyOverwritingIt()
    {
        var state = new AllianceProposalCenterState();
        state.SetSlot(0, 2, 3);
        state.SetSlot(1, 0, 1);

        AllianceProposalCenterEventMap.Apply(AllianceProposalCenterEventMap.FinalizeNewAllianceEventCode,
            Payload(0, 1), state, Logger);

        Assert.Equal(((byte?)2, (byte?)3), state.GetSlot(0));
        Assert.Equal(((byte?)0, (byte?)1), state.GetSlot(1));
    }

    [Fact]
    public void Apply_Finalize_OutOfRangeTribe_MutatesNothing()
    {
        var state = new AllianceProposalCenterState();

        AllianceProposalCenterEventMap.Apply(AllianceProposalCenterEventMap.FinalizeNewAllianceEventCode,
            Payload(0, 9), state, Logger);

        Assert.True(state.SlotIsEmpty(0));
        Assert.True(state.SlotIsEmpty(1));
    }

    [Theory]
    [InlineData(46)]
    [InlineData(47)]
    [InlineData(49)]
    public void Apply_OutOfRangeSecondTribe_MutatesNothing(int eventCode)
    {
        var state = new AllianceProposalCenterState();

        AllianceProposalCenterEventMap.Apply(eventCode, Payload(0, 9, 20260101, 20260102), state, Logger);

        Assert.True(state.SlotIsEmpty(0));
        Assert.True(state.SlotIsEmpty(1));
        Assert.Equal(AlliancePossibleInfo.Cleared, state.GetPossibleAllianceInfo(0));
    }

    [Fact]
    public void Apply_BreakViaRitual_SlotZeroMatchesPair_SelectsSlotZero_ArmsCooldowns_ClearsSlot()
    {
        var state = new AllianceProposalCenterState();
        state.SetSlot(0, 1, 2);

        AllianceProposalCenterEventMap.Apply(AllianceProposalCenterEventMap.BreakAllianceViaRitualEventCode,
            Payload(2, 1, 20260815, 20260816),
            state, Logger);

        Assert.True(state.SlotIsEmpty(0));

        var infoTribe2 = state.GetPossibleAllianceInfo(2);
        Assert.True(infoTribe2.CooldownActive);
        Assert.Equal(20260815, infoTribe2.ExpiryDateYyyyMmDd);

        var infoTribe1 = state.GetPossibleAllianceInfo(1);
        Assert.True(infoTribe1.CooldownActive);
        Assert.Equal(20260816, infoTribe1.ExpiryDateYyyyMmDd);
    }

    [Fact]
    public void Apply_BreakViaRitual_SlotZeroDoesNotMatch_FallsBackToSlotOne_UnconditionallyClearingIt()
    {
        var state = new AllianceProposalCenterState();
        state.SetSlot(0, 3, 0);
        state.SetSlot(1, 1, 2);

        AllianceProposalCenterEventMap.Apply(AllianceProposalCenterEventMap.BreakAllianceViaRitualEventCode,
            Payload(1, 2, 20260901, 20260902), state, Logger);

        Assert.Equal(((byte?)3, (byte?)0), state.GetSlot(0));
        Assert.True(state.SlotIsEmpty(1));
    }

    [Fact]
    public void Apply_BreakViaStoneCapture_IdenticalMutationShapeToBreakViaRitual()
    {
        var viaRitual = new AllianceProposalCenterState();
        viaRitual.SetSlot(0, 1, 2);
        var viaStone = new AllianceProposalCenterState();
        viaStone.SetSlot(0, 1, 2);

        var payload = Payload(1, 2, 20260701, 20260702);

        AllianceProposalCenterEventMap.Apply(AllianceProposalCenterEventMap.BreakAllianceViaRitualEventCode,
            payload, viaRitual, Logger);
        AllianceProposalCenterEventMap.Apply(AllianceProposalCenterEventMap.BreakAllianceViaStoneCaptureEventCode,
            payload, viaStone, Logger);

        Assert.Equal(viaRitual.GetSlot(0), viaStone.GetSlot(0));
        Assert.Equal(viaRitual.GetSlot(1), viaStone.GetSlot(1));
        Assert.Equal(viaRitual.GetPossibleAllianceInfo(1), viaStone.GetPossibleAllianceInfo(1));
        Assert.Equal(viaRitual.GetPossibleAllianceInfo(2), viaStone.GetPossibleAllianceInfo(2));
    }

    [Fact]
    public void Apply_AllianceStoneUnderAttack_IsABareNoOp()
    {
        var state = new AllianceProposalCenterState();
        state.SetSlot(0, 1, 2);
        state.SetPossibleAllianceCooldown(3, 20260101);

        AllianceProposalCenterEventMap.Apply(AllianceProposalCenterEventMap.AllianceStoneUnderAttackEventCode,
            Payload(0, 1, 3), state, Logger);

        Assert.Equal(((byte?)1, (byte?)2), state.GetSlot(0));
        Assert.True(state.SlotIsEmpty(1));
        var info = state.GetPossibleAllianceInfo(3);
        Assert.True(info.CooldownActive);
        Assert.Equal(20260101, info.ExpiryDateYyyyMmDd);
    }

    [Fact]
    public void Apply_UnrecognizedEventCode_MutatesNothing()
    {
        var state = new AllianceProposalCenterState();

        AllianceProposalCenterEventMap.Apply(999, Payload(0, 1, 20260101, 20260102), state, Logger);

        Assert.True(state.SlotIsEmpty(0));
        Assert.True(state.SlotIsEmpty(1));
    }

    [Fact]
    public void EventCodeRange_CoversExactlyFortySixThroughFortyNine()
    {
        Assert.Equal(46, AllianceProposalCenterEventMap.EventCodeRangeStart);
        Assert.Equal(49, AllianceProposalCenterEventMap.EventCodeRangeEnd);
        Assert.Equal(46, AllianceProposalCenterEventMap.FinalizeNewAllianceEventCode);
        Assert.Equal(47, AllianceProposalCenterEventMap.BreakAllianceViaRitualEventCode);
        Assert.Equal(48, AllianceProposalCenterEventMap.AllianceStoneUnderAttackEventCode);
        Assert.Equal(49, AllianceProposalCenterEventMap.BreakAllianceViaStoneCaptureEventCode);
    }
}
