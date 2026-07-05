using Fenrir.Application.Game.Domain.Progression;
using Fenrir.Data.Abstractions.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class TowerWarStateTests
{
    [Fact]
    public void FreshState_EveryTowerIsUntouchedInvalidAndDormant()
    {
        var state = new TowerWarState();

        for (var i = 0; i < TowerWarState.TowerCount; i++)
        {
            Assert.Equal(0, state.GetPackedState(i));
            Assert.False(state.IsValid(i));
            Assert.Equal(TowerSiegePhase.Dormant, state.GetPhase(i));
            Assert.Null(state.GetControllingTribe(i));
        }
    }

    [Theory]
    [InlineData(201, 2, 1)]
    [InlineData(402, 4, 2)]
    [InlineData(603, 6, 3)]
    [InlineData(801, 8, 1)]
    public void DecodeLevelAndType_SplitThePackedValue(int packed, int expectedLevel, int expectedType)
    {
        Assert.Equal(expectedLevel, TowerWarState.DecodeLevel(packed));
        Assert.Equal(expectedType, TowerWarState.DecodeType(packed));
    }

    [Fact]
    public void PackedValueBelowOne_DecodesToZeroLevelAndType()
    {
        Assert.Equal(0, TowerWarState.DecodeLevel(0));
        Assert.Equal(0, TowerWarState.DecodeType(0));
    }

    [Fact]
    public void SetTowerState_IsReflectedByGetters()
    {
        var state = new TowerWarState();

        state.SetTowerState(5, 201, true);

        Assert.Equal(201, state.GetPackedState(5));
        Assert.True(state.IsValid(5));
        Assert.Equal(TowerSiegePhase.Active, state.GetPhase(5));
        Assert.Equal(0, state.GetPackedState(4));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void GuardianServerIndex_IsNegativeAndNeverCollidesAcrossTowers(int towerIndex)
    {
        var index = TowerWarState.GuardianServerIndex(towerIndex);

        Assert.True(index < 0);
        Assert.Equal(-(towerIndex + 1), index);
    }

    [Fact]
    public void BeginUpgrade_ClearsValid_QueuesPendingState_ButLeavesThePackedStateUntouchedUntilCompleteUpgrade()
    {
        var state = new TowerWarState();
        state.SetTowerState(3, 201, true);

        state.BeginUpgrade(3, 401, controllingTribeId: 2);

        Assert.False(state.IsValid(3));
        Assert.Equal(201, state.GetPackedState(3)); // unchanged -- guardian hasn't (re)spawned yet
        Assert.Equal(TowerSiegePhase.Building, state.GetPhase(3));
        Assert.Equal(401, state.GetPendingPackedStateForBuilding(3));
    }

    [Fact]
    public void CompleteUpgrade_PromotesThePendingStateToLive_ArmsForTheNextUpgrade_AndMarksDirty()
    {
        var state = new TowerWarState();
        state.SetTowerState(3, 201, true);
        state.BeginUpgrade(3, 401, controllingTribeId: 2);

        state.CompleteUpgrade(3);

        Assert.Equal(401, state.GetPackedState(3));
        Assert.True(state.IsValid(3));
        Assert.Equal(TowerSiegePhase.Active, state.GetPhase(3));
        Assert.Equal((byte?)2, state.GetControllingTribe(3));
        Assert.Equal(0, state.GetPendingPackedStateForBuilding(3));
    }

    [Fact]
    public void CompleteUpgrade_WithNoPendingUpgrade_IsANoOp()
    {
        var state = new TowerWarState();
        state.SetTowerState(3, 201, true);

        state.CompleteUpgrade(3);

        Assert.Equal(201, state.GetPackedState(3));
        Assert.True(state.IsValid(3));
    }

    [Fact]
    public void BeginSiege_ClearsValid_AndMovesToSiegedPhase_ButKeepsTheLastKnownPackedState()
    {
        var state = new TowerWarState();
        state.SetTowerState(3, 401, true);

        state.BeginSiege(3, DateTime.UtcNow);

        Assert.False(state.IsValid(3));
        Assert.Equal(401, state.GetPackedState(3));
        Assert.Equal(TowerSiegePhase.Sieged, state.GetPhase(3));
    }

    [Fact]
    public void BeginSiege_CalledTwice_KeepsTheOriginalSiegeStart()
    {
        var state = new TowerWarState();
        state.SetTowerState(3, 401, true);
        var first = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        state.BeginSiege(3, first);
        state.BeginSiege(3, first + TimeSpan.FromMinutes(10));

        Assert.False(state.IsDueForDestruction(3, first + TowerWarState.SiegeCollapseCooldown - TimeSpan.FromSeconds(1)));
        Assert.True(state.IsDueForDestruction(3, first + TowerWarState.SiegeCollapseCooldown));
    }

    [Fact]
    public void IsDueForDestruction_FalseBeforeCooldownElapses_TrueAtOrAfter()
    {
        var state = new TowerWarState();
        state.SetTowerState(3, 401, true);
        var start = DateTime.UtcNow;
        state.BeginSiege(3, start);

        Assert.False(state.IsDueForDestruction(3, start + TimeSpan.FromMinutes(4)));
        Assert.True(state.IsDueForDestruction(3, start + TowerWarState.SiegeCollapseCooldown));
    }

    [Fact]
    public void IsDueForDestruction_TowerNotSieged_IsAlwaysFalse()
    {
        var state = new TowerWarState();
        state.SetTowerState(3, 401, true);

        Assert.False(state.IsDueForDestruction(3, DateTime.UtcNow.AddYears(1)));
    }

    [Fact]
    public void CompleteDestruction_RevertsEverythingToDormant_AndMarksDirty()
    {
        var state = new TowerWarState();
        state.SetTowerState(3, 401, true);
        state.BeginSiege(3, DateTime.UtcNow);

        state.CompleteDestruction(3);

        Assert.Equal(0, state.GetPackedState(3));
        Assert.False(state.IsValid(3));
        Assert.Null(state.GetControllingTribe(3));
        Assert.Equal(TowerSiegePhase.Dormant, state.GetPhase(3));
    }

    [Fact]
    public async Task InitializeAsync_BootstrapsThenHydrates_BuiltTowersResumeIntoBuilding()
    {
        var repository = new FakeTowerRepository
        {
            Rows =
            [
                new TowerStateRowDto(0, 0, 0, null, null),
                new TowerStateRowDto(1, 4, 2, 1, DateTime.UtcNow),
                .. Enumerable.Range(2, 10).Select(i => new TowerStateRowDto((byte)i, 0, 0, null, null))
            ]
        };
        var state = new TowerWarState();

        await state.InitializeAsync(repository, CancellationToken.None);

        Assert.Equal(1, repository.EnsureInitializedCallCount);

        // Untouched tower: stays Dormant.
        Assert.Equal(TowerSiegePhase.Dormant, state.GetPhase(0));
        Assert.Equal(0, state.GetPackedState(0));

        // Previously-built tower: resumes into Building (its guardian MonsterEntity never survived the
        // restart) with the persisted level/type queued as the pending target.
        Assert.Equal(TowerSiegePhase.Building, state.GetPhase(1));
        Assert.Equal(402, state.GetPendingPackedStateForBuilding(1));
        Assert.Equal((byte?)1, state.GetControllingTribe(1));
    }

    [Fact]
    public async Task FlushDirtyAsync_OnlyPersistsTowersTouchedSinceTheLastFlush()
    {
        var repository = new FakeTowerRepository();
        var state = new TowerWarState();
        state.SetTowerState(3, 201, true);
        state.BeginUpgrade(3, 401, controllingTribeId: 2);
        state.CompleteUpgrade(3); // only this one is marked dirty

        await state.FlushDirtyAsync(repository, CancellationToken.None);

        var call = Assert.Single(repository.SetProgressCalls);
        Assert.Equal((byte)3, call.TowerIndex);
        Assert.Equal((byte)4, call.Level);
        Assert.Equal((byte)1, call.TowerType);
        Assert.Equal((byte?)2, call.ControllingTribeId);
    }

    [Fact]
    public async Task FlushDirtyAsync_NothingDirty_NeverCallsTheRepository()
    {
        var repository = new FakeTowerRepository();
        var state = new TowerWarState();

        await state.FlushDirtyAsync(repository, CancellationToken.None);

        Assert.Empty(repository.SetProgressCalls);
    }

    [Fact]
    public async Task FlushDirtyAsync_RepositoryThrows_LeavesTheTowerDirtyForNextInterval_AndNeverThrows()
    {
        var repository = new FakeTowerRepository { ThrowOnSetProgress = true };
        var state = new TowerWarState();
        state.SetTowerState(3, 201, true);
        state.BeginUpgrade(3, 401, controllingTribeId: 2);
        state.CompleteUpgrade(3);

        await state.FlushDirtyAsync(repository, CancellationToken.None); // must not throw
        repository.ThrowOnSetProgress = false;
        await state.FlushDirtyAsync(repository, CancellationToken.None); // retried and now succeeds

        Assert.Single(repository.SetProgressCalls);
    }
}
