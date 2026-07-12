using Fenrir.Application.Game.Domain.Social.Duel;

namespace Fenrir.Application.Game.Tests.Social;

public class DuelRegistryTests
{
    [Fact]
    public void FullLifecycle_AskAcceptStart_ArmsBothSidesWithSharedUniqueNumber()
    {
        var registry = new DuelRegistry();

        Assert.Equal(DuelAskOutcome.Sent, registry.TryAsk(1, 2, true));
        Assert.True(registry.TryAnswer(2, true, out var challengerId));
        Assert.Equal(1, challengerId);

        Assert.True(registry.TryStart(1, out var duel));

        Assert.Equal(1, duel.PlayerA);
        Assert.Equal(2, duel.PlayerB);
        Assert.True(duel.NoPotions);
        Assert.True(registry.TryGetActiveDuel(1, out var fromA));
        Assert.True(registry.TryGetActiveDuel(2, out var fromB));
        Assert.Same(fromA, fromB);
    }

    [Fact]
    public void TryStart_CallableByEitherAcceptedSide()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);
        registry.TryAnswer(2, true, out _);

        Assert.True(registry.TryStart(1, out var duel));
        Assert.False(duel.NoPotions);
    }

    [Fact]
    public void TryAsk_NegotiatingChallenger_ReturnsBusy()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);

        Assert.Equal(DuelAskOutcome.ChallengerBusy, registry.TryAsk(1, 3, false));
    }

    [Fact]
    public void TryAsk_ChallengerActivelyDueling_ReturnsChallengerAlreadyDueling_DistinctFromOrdinaryBusy()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);
        registry.TryAnswer(2, true, out _);
        registry.TryStart(1, out _);

        Assert.Equal(DuelAskOutcome.ChallengerAlreadyDueling, registry.TryAsk(1, 3, false));
    }

    [Fact]
    public void TryAsk_TargetActivelyDueling_StillReturnsOrdinaryTargetBusy()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);
        registry.TryAnswer(2, true, out _);
        registry.TryStart(1, out _);

        Assert.Equal(DuelAskOutcome.TargetBusy, registry.TryAsk(4, 2, false));
    }

    [Fact]
    public void TryEndActiveDuel_RemovesBothSides()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);
        registry.TryAnswer(2, true, out _);
        registry.TryStart(1, out _);

        var ended = registry.TryEndActiveDuel(1, out var opponentId);

        Assert.True(ended);
        Assert.Equal(2, opponentId);
        Assert.False(registry.TryGetActiveDuel(1, out _));
        Assert.False(registry.TryGetActiveDuel(2, out _));
    }

    [Fact]
    public void TryCancel_ClearsPendingAskForBothSides()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);

        Assert.True(registry.TryCancel(1, out var targetId));
        Assert.Equal(2, targetId);
        Assert.Equal(DuelAskOutcome.Sent, registry.TryAsk(1, 3, false));
        Assert.Equal(DuelAskOutcome.Sent, registry.TryAsk(4, 2, false));
    }

    [Fact]
    public void TryAnswer_Refused_DoesNotArmStart()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);

        Assert.True(registry.TryAnswer(2, false, out _));
        Assert.False(registry.TryStart(1, out _));
        Assert.False(registry.TryStart(2, out _));
    }

    [Fact]
    public void TryStart_SeedsRemainingTicksAtDurationTicks()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);
        registry.TryAnswer(2, true, out _);

        Assert.True(registry.TryStart(1, out var duel));

        Assert.Equal(DuelRegistry.DurationTicks, duel.RemainingTicks);
        Assert.Equal(180, DuelRegistry.DurationTicks);
    }

    [Fact]
    public void ForceClearOnZoneEntry_ActiveDuel_RemovesOnlyTheEnteringCharactersOwnKey()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);
        registry.TryAnswer(2, true, out _);
        registry.TryStart(1, out _);

        registry.ForceClearOnZoneEntry(1);

        Assert.False(registry.TryGetActiveDuel(1, out _));
        Assert.True(registry.TryGetActiveDuel(2, out var stillThere));
        Assert.NotNull(stillThere);
    }

    [Fact]
    public void ForceClearOnZoneEntry_PendingAsk_ClearsBothDirections()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);

        registry.ForceClearOnZoneEntry(2);

        Assert.Equal(DuelAskOutcome.Sent, registry.TryAsk(1, 3, false));
        Assert.Equal(DuelAskOutcome.Sent, registry.TryAsk(4, 2, false));
    }

    [Fact]
    public void ForceClearOnZoneEntry_AcceptedButNeverStarted_ClearsBothSidesSilently()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);
        registry.TryAnswer(2, true, out _);

        registry.ForceClearOnZoneEntry(1);

        Assert.False(registry.TryStart(1, out _));
        Assert.False(registry.TryStart(2, out _));
        Assert.Equal(DuelAskOutcome.Sent, registry.TryAsk(2, 5, false));
    }

    [Fact]
    public void ForceClearOnZoneEntry_NoStaleState_IsANoOp()
    {
        var registry = new DuelRegistry();

        registry.ForceClearOnZoneEntry(42);

        Assert.Equal(DuelAskOutcome.Sent, registry.TryAsk(42, 43, false));
    }

    [Fact]
    public void TryClearAcceptedForDisconnect_PartnerDisconnects_UnsticksTheSurvivor()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);
        registry.TryAnswer(2, true, out _);

        Assert.True(registry.TryClearAcceptedForDisconnect(2, out var partnerId));
        Assert.Equal(1, partnerId);

        Assert.Equal(DuelAskOutcome.Sent, registry.TryAsk(1, 3, false));
        Assert.False(registry.TryStart(1, out _));
        Assert.False(registry.TryStart(2, out _));
    }

    [Fact]
    public void TryClearAcceptedForDisconnect_NoAcceptedState_ReturnsFalse()
    {
        var registry = new DuelRegistry();

        Assert.False(registry.TryClearAcceptedForDisconnect(1, out _));
    }
}
