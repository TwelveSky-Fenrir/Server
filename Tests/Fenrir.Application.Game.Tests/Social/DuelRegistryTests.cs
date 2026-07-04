using Fenrir.Application.Game.Social.Duel;

namespace Fenrir.Application.Game.Tests.Social;

public class DuelRegistryTests
{
    [Fact]
    public void FullLifecycle_AskAcceptStart_ArmsBothSidesWithSharedUniqueNumber()
    {
        var registry = new DuelRegistry();

        Assert.Equal(DuelAskOutcome.Sent, registry.TryAsk(1, 2, noPotions: true));
        Assert.True(registry.TryAnswer(2, accepted: true, out var challengerId));
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
        registry.TryAsk(1, 2, noPotions: false);
        registry.TryAnswer(2, true, out _);

        // The CHALLENGER (not the accepter) calls start -- still succeeds (symmetric acceptance).
        Assert.True(registry.TryStart(1, out var duel));
        Assert.False(duel.NoPotions);
    }

    [Fact]
    public void TryAsk_AlreadyDuelling_ReturnsBusy()
    {
        var registry = new DuelRegistry();
        registry.TryAsk(1, 2, false);
        registry.TryAnswer(2, true, out _);
        registry.TryStart(1, out _);

        Assert.Equal(DuelAskOutcome.ChallengerBusy, registry.TryAsk(1, 3, false));
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

        Assert.True(registry.TryAnswer(2, accepted: false, out _));
        Assert.False(registry.TryStart(1, out _));
        Assert.False(registry.TryStart(2, out _));
    }
}
