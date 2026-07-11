using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class TowerWarStateConstructionTests
{
    private static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);


    [Fact]
    public void BeginConstruction_OnFullyIdleTower_RecordsKind_AndStaysDormant()
    {
        var state = new TowerWarState();

        Assert.True(state.BeginConstruction(0, 2, 3));

        Assert.Equal(2, state.GetPendingConstructKind(0));
        Assert.Equal(TowerSiegePhase.Dormant, state.GetPhase(0));
        Assert.False(state.IsValid(0));
        Assert.Equal(0, state.GetPackedState(0));
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
        Assert.False(state.BeginConstruction(0, 2, 1));
        Assert.Equal(1, state.GetPendingConstructKind(0));
    }

    [Fact]
    public void CancelConstruction_ReturnsSlotToIdle_AllowingAFreshClaim()
    {
        var state = new TowerWarState();
        state.BeginConstruction(0, 1, 0);

        state.CancelConstruction(0);

        Assert.Equal(0, state.GetPendingConstructKind(0));
        Assert.True(state.BeginConstruction(0, 3, 2));
    }

    [Fact]
    public void CancelConstruction_AfterTheGuardianHasSpawned_IsANoOp()
    {
        var state = new TowerWarState();
        state.BeginConstruction(0, 1, 0);
        state.CompleteConstructionSpawn(0, T0);

        state.CancelConstruction(0);

        Assert.Equal(201, state.GetPackedState(0));
    }


    [Fact]
    public void CompleteConstructionSpawn_PromotesToLevel1OfTheKind_StartsCooldown_StaysInvalid()
    {
        var state = new TowerWarState();
        state.BeginConstruction(0, 2, 3);

        state.CompleteConstructionSpawn(0, T0);

        Assert.Equal(202, state.GetPackedState(0));
        Assert.Equal(2, TowerWarState.DecodeLevel(202));
        Assert.Equal(2, TowerWarState.DecodeType(202));
        Assert.Equal((byte?)3, state.GetControllingTribe(0));
        Assert.Equal(0, state.GetPendingConstructKind(0));
        Assert.False(state.IsValid(0));
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


    [Fact]
    public void CountTowersOfKind_CountsBothBuiltAndStillConstructingTowers_ClusterWide()
    {
        var state = new TowerWarState();
        state.SetTowerState(0, 201, true);
        state.SetTowerState(4, 401, true);
        state.BeginConstruction(8, 1, 2);
        state.SetTowerState(5, 202, true);

        Assert.Equal(3, state.CountTowersOfKind(1));
        Assert.Equal(1, state.CountTowersOfKind(2));
        Assert.Equal(0, state.CountTowersOfKind(3));
    }

    [Fact]
    public void IsKindPresentInTribeGroup_DetectsASiblingSlotOfTheSameKind_AndExcludesSelf()
    {
        var state = new TowerWarState();
        state.SetTowerState(1, 201, true);

        Assert.True(state.IsKindPresentInTribeGroup(0, 1));
        Assert.False(state.IsKindPresentInTribeGroup(0, 2));
    }

    [Fact]
    public void IsKindPresentInTribeGroup_IgnoresTheSameKindInAnotherTribeGroup()
    {
        var state = new TowerWarState();
        state.SetTowerState(3, 201, true);

        Assert.False(state.IsKindPresentInTribeGroup(0, 1));
    }

    [Fact]
    public void IsKindPresentInTribeGroup_DetectsAStillConstructingSibling()
    {
        var state = new TowerWarState();
        state.BeginConstruction(2, 3, 0);

        Assert.True(state.IsKindPresentInTribeGroup(0, 3));
    }


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
        state.RecordGuardianHit(0, T0);
        Assert.False(state.IsUnderAttack(0));

        Assert.False(state.TryResetIdleAttackState(0, T0 + TimeSpan.FromSeconds(29)));
        Assert.True(state.TryResetIdleAttackState(0, T0 + TowerWarState.AttackStateIdleReset));
        Assert.True(state.IsUnderAttack(0));
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
        Assert.True(state.RecordGuardianHit(0, T0));
        Assert.False(state.RecordGuardianHit(0, T0 + TimeSpan.FromMinutes(1)));

        Assert.False(state.TryClearStaleEngagement(0, T0 + TimeSpan.FromMinutes(9)));
        Assert.True(state.TryClearStaleEngagement(0, T0 + TowerWarState.EngagementAutoClear));
        Assert.Null(state.GetFirstAttackAtUtc(0));

        Assert.True(state.RecordGuardianHit(0, T0 + TimeSpan.FromMinutes(11)));
    }


    [Fact]
    public void RequestGuardianHeal_SetsAndTryConsumeDrainsExactlyOnce()
    {
        var state = new TowerWarState();

        state.RequestGuardianHeal(0);
        Assert.True(state.IsGuardianHealPending(0));

        Assert.True(state.TryConsumeGuardianHeal(0));
        Assert.False(state.IsGuardianHealPending(0));
        Assert.False(state.TryConsumeGuardianHeal(0));
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

        Assert.True(state.BeginConstruction(0, 2, 1));
    }
}
