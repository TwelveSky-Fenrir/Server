using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

/// <summary>
///     A11 -- the from-scratch construction lifecycle (item 665), the item-667 heal handoff primitives, and the
///     tower-attack AI idle timers that <see cref="TowerWarStateTests" /> (the upgrade/siege half) does not cover.
///     All time-driven transitions are exercised at this deterministic level (an explicit <c>utcNow</c> argument),
///     leaving <see cref="TowerLifecycleSystemTests" /> to prove the tick system merely forwards <c>DateTime.UtcNow</c>.
/// </summary>
public class TowerWarStateConstructionTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    // ---- BeginConstruction / CancelConstruction ---------------------------------------------------------------

    [Fact]
    public void BeginConstruction_OnFullyIdleTower_RecordsKind_AndStaysDormant()
    {
        var state = new TowerWarState();

        Assert.True(state.BeginConstruction(0, 2, 3));

        Assert.Equal(2, state.GetPendingConstructKind(0));
        Assert.Equal(TowerSiegePhase.Dormant, state.GetPhase(0));
        Assert.False(state.IsValid(0));
        Assert.Equal(0, state.GetPackedState(0)); // packed stays 0 until the guardian actually spawns
    }

    [Fact]
    public void BeginConstruction_OnAlreadyBuiltTower_ReturnsFalse_ChangingNothing()
    {
        var state = new TowerWarState();
        state.SetTowerState(0, 201, true);

        Assert.False(state.BeginConstruction(0, 1, 0));
        Assert.Equal(0, state.GetPendingConstructKind(0));
    }

    [Fact]
    public void BeginConstruction_OnTowerWithPendingUpgrade_ReturnsFalse()
    {
        var state = new TowerWarState();
        state.SetTowerState(0, 201, true);
        state.BeginUpgrade(0, 401, 0);

        Assert.False(state.BeginConstruction(0, 1, 0));
    }

    [Fact]
    public void BeginConstruction_OnSiegedTower_ReturnsFalse()
    {
        var state = new TowerWarState();
        state.SetTowerState(0, 201, true);
        state.BeginSiege(0, T0);

        Assert.False(state.BeginConstruction(0, 1, 0));
    }

    [Fact]
    public void BeginConstruction_SecondConcurrentClaimOnSameTower_LosesTheRace()
    {
        var state = new TowerWarState();

        Assert.True(state.BeginConstruction(0, 1, 0));
        Assert.False(state.BeginConstruction(0, 2, 1)); // slot already claimed -- the loser must retain its item
        Assert.Equal(1, state.GetPendingConstructKind(0)); // first claim's kind survives
    }

    [Fact]
    public void CancelConstruction_ReturnsSlotToIdle_AllowingAFreshClaim()
    {
        var state = new TowerWarState();
        state.BeginConstruction(0, 1, 0);

        state.CancelConstruction(0);

        Assert.Equal(0, state.GetPendingConstructKind(0));
        Assert.True(state.BeginConstruction(0, 3, 2)); // idle again
    }

    [Fact]
    public void CancelConstruction_AfterTheGuardianHasSpawned_IsANoOp()
    {
        var state = new TowerWarState();
        state.BeginConstruction(0, 1, 0);
        state.CompleteConstructionSpawn(0, T0); // past the point a plain cancel is safe

        state.CancelConstruction(0);

        Assert.Equal(201, state.GetPackedState(0)); // level-1 tower untouched
    }

    // ---- CompleteConstructionSpawn / create-cooldown ----------------------------------------------------------

    [Fact]
    public void CompleteConstructionSpawn_PromotesToLevel1OfTheKind_StartsCooldown_StaysInvalid()
    {
        var state = new TowerWarState();
        state.BeginConstruction(0, 2, 3);

        state.CompleteConstructionSpawn(0, T0);

        Assert.Equal(202, state.GetPackedState(0)); // 200 + kind
        Assert.Equal(2, TowerWarState.DecodeLevel(202)); // level 1 == raw digit 2
        Assert.Equal(2, TowerWarState.DecodeType(202)); // kind 2
        Assert.Equal((byte?)3, state.GetControllingTribe(0));
        Assert.Equal(0, state.GetPendingConstructKind(0)); // kind now lives in the packed state, cooldown started
        Assert.False(state.IsValid(0)); // not attackable until the create-cooldown elapses
        Assert.Equal(TowerSiegePhase.Dormant, state.GetPhase(0));
    }

    [Fact]
    public void CompleteConstructionSpawn_WithNoConstructionArmed_IsANoOp()
    {
        var state = new TowerWarState();

        state.CompleteConstructionSpawn(0, T0);

        Assert.Equal(0, state.GetPackedState(0));
    }

    [Fact]
    public void CompleteConstructionSpawn_ResetsGuardianHitBookkeeping_ForTheFreshGuardian()
    {
        var state = new TowerWarState();
        state.RecordGuardianHit(0, T0);
        state.BeginConstruction(0, 1, 0);

        state.CompleteConstructionSpawn(0, T0);

        Assert.Null(state.GetFirstAttackAtUtc(0));
        Assert.Null(state.GetLastAttackAtUtc(0));
        Assert.False(state.IsUnderAttack(0));
    }

    [Fact]
    public void CreateCooldown_ElapsesAtOrAfterFiveMinutes_ThenCompletionMakesTheTowerActive()
    {
        var state = new TowerWarState();
        state.BeginConstruction(0, 1, 0);
        state.CompleteConstructionSpawn(0, T0);

        Assert.False(state.IsCreateCooldownElapsed(0, T0 + TimeSpan.FromMinutes(4)));
        Assert.True(state.IsCreateCooldownElapsed(0, T0 + TowerWarState.CreateCooldown));

        state.CompleteConstructionCooldown(0);

        Assert.True(state.IsValid(0));
        Assert.Equal(TowerSiegePhase.Active, state.GetPhase(0));
    }

    [Fact]
    public void CompleteConstructionCooldown_WithNoCooldownRunning_IsANoOp()
    {
        var state = new TowerWarState();

        state.CompleteConstructionCooldown(0);

        Assert.False(state.IsValid(0));
    }

    // ---- CountTowersOfKind / IsKindPresentInTribeGroup --------------------------------------------------------

    [Fact]
    public void CountTowersOfKind_CountsBothBuiltAndStillConstructingTowers_ClusterWide()
    {
        var state = new TowerWarState();
        state.SetTowerState(0, 201, true); // kind 1, built
        state.SetTowerState(4, 401, true); // kind 1, built (a different tribe group)
        state.BeginConstruction(8, 1, 2); // kind 1, still creating
        state.SetTowerState(5, 202, true); // kind 2, built

        Assert.Equal(3, state.CountTowersOfKind(1));
        Assert.Equal(1, state.CountTowersOfKind(2));
        Assert.Equal(0, state.CountTowersOfKind(3));
    }

    [Fact]
    public void IsKindPresentInTribeGroup_DetectsASiblingSlotOfTheSameKind_AndExcludesSelf()
    {
        var state = new TowerWarState();
        state.SetTowerState(1, 201, true); // tower 0's own 3-slot group is {0,1,2}

        Assert.True(state.IsKindPresentInTribeGroup(0, 1));
        Assert.False(state.IsKindPresentInTribeGroup(0, 2)); // no slot in the group holds kind 2
    }

    [Fact]
    public void IsKindPresentInTribeGroup_IgnoresTheSameKindInAnotherTribeGroup()
    {
        var state = new TowerWarState();
        state.SetTowerState(3, 201, true); // slot 3 is tribe 1's group {3,4,5}, not tower 0's

        Assert.False(state.IsKindPresentInTribeGroup(0, 1));
    }

    [Fact]
    public void IsKindPresentInTribeGroup_DetectsAStillConstructingSibling()
    {
        var state = new TowerWarState();
        state.BeginConstruction(2, 3, 0); // slot 2 shares tower 0's group

        Assert.True(state.IsKindPresentInTribeGroup(0, 3));
    }

    // ---- Attack-AI idle timers --------------------------------------------------------------------------------

    [Fact]
    public void TryResetIdleAttackState_WithNoHitYet_ReturnsFalse()
    {
        var state = new TowerWarState();

        Assert.False(state.TryResetIdleAttackState(0, T0));
    }

    [Fact]
    public void TryResetIdleAttackState_OnlyFiresOnceThe30sIdleWindowHasElapsed()
    {
        var state = new TowerWarState();
        state.RecordGuardianHit(0, T0); // a landed hit clears the siege flag
        Assert.False(state.IsUnderAttack(0));

        Assert.False(state.TryResetIdleAttackState(0, T0 + TimeSpan.FromSeconds(29)));
        Assert.True(state.TryResetIdleAttackState(0, T0 + TowerWarState.AttackStateIdleReset));
        Assert.True(state.IsUnderAttack(0)); // attack-state returned to "ready"
    }

    [Fact]
    public void TryResetIdleAttackState_WhenAlreadyReset_ReturnsFalse()
    {
        var state = new TowerWarState();
        state.RecordGuardianHit(0, T0);
        Assert.True(state.TryResetIdleAttackState(0, T0 + TowerWarState.AttackStateIdleReset));

        Assert.False(state.TryResetIdleAttackState(0, T0 + TimeSpan.FromMinutes(1)));
    }

    [Fact]
    public void TryClearStaleEngagement_WithNoEngagement_ReturnsFalse()
    {
        var state = new TowerWarState();

        Assert.False(state.TryClearStaleEngagement(0, T0));
    }

    [Fact]
    public void TryClearStaleEngagement_After10Minutes_ClearsFirstHitTracking_ReArmingTheOneTimeNotice()
    {
        var state = new TowerWarState();
        Assert.True(state.RecordGuardianHit(0, T0)); // first hit of this engagement
        Assert.False(state.RecordGuardianHit(0, T0 + TimeSpan.FromMinutes(1)));

        Assert.False(state.TryClearStaleEngagement(0, T0 + TimeSpan.FromMinutes(9)));
        Assert.True(state.TryClearStaleEngagement(0, T0 + TowerWarState.EngagementAutoClear));
        Assert.Null(state.GetFirstAttackAtUtc(0));

        // A hit after the auto-clear is treated as the first hit of a fresh engagement again.
        Assert.True(state.RecordGuardianHit(0, T0 + TimeSpan.FromMinutes(11)));
    }

    // ---- item-667 heal handoff primitives ---------------------------------------------------------------------

    [Fact]
    public void RequestGuardianHeal_SetsAndTryConsumeDrainsExactlyOnce()
    {
        var state = new TowerWarState();

        state.RequestGuardianHeal(0);
        Assert.True(state.IsGuardianHealPending(0));

        Assert.True(state.TryConsumeGuardianHeal(0));
        Assert.False(state.IsGuardianHealPending(0));
        Assert.False(state.TryConsumeGuardianHeal(0)); // nothing left to drain
    }

    [Fact]
    public void RequestGuardianHeal_IsIdempotent_TwoRequestsDrainAsOne()
    {
        var state = new TowerWarState();

        state.RequestGuardianHeal(0);
        state.RequestGuardianHeal(0);

        Assert.True(state.TryConsumeGuardianHeal(0));
        Assert.False(state.TryConsumeGuardianHeal(0));
    }

    [Fact]
    public void TryConsumeGuardianHeal_OnlyTouchesTheAddressedTower()
    {
        var state = new TowerWarState();
        state.RequestGuardianHeal(0);

        Assert.False(state.TryConsumeGuardianHeal(1));
        Assert.True(state.IsGuardianHealPending(0));
    }

    // ---- destroy fully resets the A11 in-flight state ---------------------------------------------------------

    [Fact]
    public void CompleteDestruction_ClearsInFlightConstruction_Cooldown_AndPendingHeal()
    {
        var state = new TowerWarState();
        state.BeginConstruction(0, 1, 0);
        state.CompleteConstructionSpawn(0, T0);
        state.RequestGuardianHeal(0);
        state.BeginSiege(0, T0);

        state.CompleteDestruction(0);

        Assert.Equal(0, state.GetPendingConstructKind(0));
        Assert.False(state.IsGuardianHealPending(0));
        Assert.Equal(0, state.GetPackedState(0));

        // A destroyed slot is fully idle again -- a fresh item-665 can start cleanly.
        Assert.True(state.BeginConstruction(0, 2, 1));
    }
}
