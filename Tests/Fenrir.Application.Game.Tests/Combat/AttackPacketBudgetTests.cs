using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Combat;

/// <summary>
///     Covers <see cref="AttackPacketBudget.TryConsume" /> -- the companion check to
///     <see cref="Fenrir.Application.Game.Domain.Movement.CharacterMotionWhitelist" /> that enforces the
///     attack sub-packet ceiling and replay guard against a character's session state.
/// </summary>
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
    public void EnforcementOff_AlwaysAccepted_AndNeverCounts()
    {
        var state = State(enforced: false, ceiling: 0, actionSort: 42);

        Assert.True(AttackPacketBudget.TryConsume(state, attackActionValue4: 999));
        Assert.True(AttackPacketBudget.TryConsume(state, attackActionValue4: 999));

        // Uncapped by explicit original intent (Sort 65/74): the used-so-far counter is never touched.
        Assert.Equal(0, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void EnforcementOn_WithinCeiling_MatchingReplayGuard_IsAccepted_AndIncrementsCounter()
    {
        var state = State(enforced: true, ceiling: 3, actionSort: 42);

        Assert.True(AttackPacketBudget.TryConsume(state, attackActionValue4: 42));

        Assert.Equal(1, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void EnforcementOn_ExceedingCeiling_IsRejected()
    {
        var state = State(enforced: true, ceiling: 2, actionSort: 42);

        Assert.True(AttackPacketBudget.TryConsume(state, attackActionValue4: 42));
        Assert.True(AttackPacketBudget.TryConsume(state, attackActionValue4: 42));
        // Third sub-packet exceeds the ceiling of 2.
        Assert.False(AttackPacketBudget.TryConsume(state, attackActionValue4: 42));

        Assert.Equal(3, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void EnforcementOn_CeilingZero_RejectsTheVeryFirstSubPacket()
    {
        // Matches the ordinary non-combat animation default: enforcement on, ceiling zero -- a hard zero, not
        // a soft cap.
        var state = State(enforced: true, ceiling: 0, actionSort: 0);

        Assert.False(AttackPacketBudget.TryConsume(state, attackActionValue4: 0));
        Assert.Equal(1, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void EnforcementOn_MismatchedReplayGuard_IsRejected_EvenWithinCeiling()
    {
        var state = State(enforced: true, ceiling: 5, actionSort: 42);

        // AttackActionValue4 must match the character's currently-recorded action category (ActionSort).
        Assert.False(AttackPacketBudget.TryConsume(state, attackActionValue4: 99));
    }

    [Fact]
    public void EnforcementOn_MismatchedReplayGuard_StillCountsTowardTheCeiling()
    {
        var state = State(enforced: true, ceiling: 5, actionSort: 42);

        AttackPacketBudget.TryConsume(state, attackActionValue4: 99);

        Assert.Equal(1, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void CountingDisabled_SkipsCeiling_ButStillEnforcesReplayGuard()
    {
        var state = State(enforced: true, ceiling: 0, actionSort: 42);

        // Ceiling is already exhausted (0), but countAttempt:false mirrors ProcessAttack05's explicit opt-out
        // of the counter/ceiling comparison -- only the replay guard still applies.
        Assert.True(AttackPacketBudget.TryConsume(state, attackActionValue4: 42, countAttempt: false));
        Assert.Equal(0, state.AttackSubPacketsUsed);

        Assert.False(AttackPacketBudget.TryConsume(state, attackActionValue4: 1, countAttempt: false));
        Assert.Equal(0, state.AttackSubPacketsUsed);
    }

    [Fact]
    public void SuccessiveConsumptions_AccumulateAcrossCalls_UntilCeilingExceeded()
    {
        var state = State(enforced: true, ceiling: 1, actionSort: 7);

        Assert.True(AttackPacketBudget.TryConsume(state, attackActionValue4: 7));
        Assert.False(AttackPacketBudget.TryConsume(state, attackActionValue4: 7));
        Assert.False(AttackPacketBudget.TryConsume(state, attackActionValue4: 7));
    }
}
