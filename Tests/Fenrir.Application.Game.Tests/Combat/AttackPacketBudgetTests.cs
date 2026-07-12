using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Combat;

public class AttackPacketBudgetTests
{
    private static PlayerRuntimeState State(bool enforced, int ceiling, int actionSort, int used = 0)
    {
        return new PlayerRuntimeState
        {
            CharacterId = 1,
            Session = ZoneTestKit.CreateSession(1).Session,
            Name = "Hero",
            Tribe = 1,
            Gender = 0,
            HeadType = 2,
            FaceType = 3,
            Level = 42,
            AttackBudgetEnforced = enforced,
            AttackSubPacketCeiling = ceiling,
            ActionSort = actionSort,
            AttackSubPacketsUsed = used
        };
    }

    [Fact]
    public void EnforcementOff_CeilingNeverCounted_ButReplayGuardStillEnforced()
    {
        var state = State(false, 0, 42);

        Assert.True(AttackPacketBudget.TryConsume(state, 42));
        Assert.True(AttackPacketBudget.TryConsume(state, 42));

        Assert.Equal(0, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void EnforcementOff_MismatchedReplayGuard_IsStillRejected()
    {
        var state = State(false, 0, 42);

        Assert.False(AttackPacketBudget.TryConsume(state, 999));
        Assert.Equal(0, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void EnforcementOn_WithinCeiling_MatchingReplayGuard_IsAccepted_AndIncrementsCounter()
    {
        var state = State(true, 3, 42);

        Assert.True(AttackPacketBudget.TryConsume(state, 42));

        Assert.Equal(1, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void EnforcementOn_ExceedingCeiling_IsRejected()
    {
        var state = State(true, 2, 42);

        Assert.True(AttackPacketBudget.TryConsume(state, 42));
        Assert.True(AttackPacketBudget.TryConsume(state, 42));
        Assert.False(AttackPacketBudget.TryConsume(state, 42));

        Assert.Equal(3, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void EnforcementOn_CeilingZero_RejectsTheVeryFirstSubPacket()
    {
        var state = State(true, 0, 0);

        Assert.False(AttackPacketBudget.TryConsume(state, 0));
        Assert.Equal(1, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void EnforcementOn_MismatchedReplayGuard_IsRejected_EvenWithinCeiling()
    {
        var state = State(true, 5, 42);

        Assert.False(AttackPacketBudget.TryConsume(state, 99));
    }

    [Fact]
    public void EnforcementOn_MismatchedReplayGuard_StillCountsTowardTheCeiling()
    {
        var state = State(true, 5, 42);

        AttackPacketBudget.TryConsume(state, 99);

        Assert.Equal(1, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void CeilingDisabledPerCall_SkipsCeiling_ButStillEnforcesReplayGuard()
    {
        var state = State(true, 0, 42);

        Assert.True(AttackPacketBudget.TryConsume(state, 42, enforceCeiling: false));
        Assert.Equal(0, state.AttackSubPacketsUsed);

        Assert.False(AttackPacketBudget.TryConsume(state, 1, enforceCeiling: false));
        Assert.Equal(0, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void SuccessiveConsumptions_AccumulateAcrossCalls_UntilCeilingExceeded()
    {
        var state = State(true, 1, 7);

        Assert.True(AttackPacketBudget.TryConsume(state, 7));
        Assert.False(AttackPacketBudget.TryConsume(state, 7));
        Assert.False(AttackPacketBudget.TryConsume(state, 7));
    }
}
