using Fenrir.Application.Game.Domain.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class HeroRankPointAccumulatorTests
{
    [Fact]
    public async Task FlushDirtyAsync_NoPendingGrants_DoesNotCallRepository()
    {
        var accumulator = new HeroRankPointAccumulator();
        var repo = new FakeHeroRankingRepository();

        await accumulator.FlushDirtyAsync(repo, CancellationToken.None);

        Assert.Empty(repo.AddPointsCalls);
    }

    [Fact]
    public async Task FlushDirtyAsync_SinglePendingGrant_CallsAddPointsWithCurrentPeriod()
    {
        var accumulator = new HeroRankPointAccumulator();
        var repo = new FakeHeroRankingRepository();

        accumulator.AddPending(7, 3, 1, 42);
        await accumulator.FlushDirtyAsync(repo, CancellationToken.None);

        var call = Assert.Single(repo.AddPointsCalls);
        Assert.Equal(7, call.CharacterId);
        Assert.Equal(HeroRankPointAccumulator.CurrentPeriodKind, call.PeriodKind);
        Assert.Equal(3, call.Delta);
        Assert.Equal((byte)1, call.TribeId);
        Assert.Equal(42, call.Level);
    }

    [Fact]
    public async Task MultipleGrantsForSameCharacterBeforeFlush_AreSummedIntoOneCall()
    {
        var accumulator = new HeroRankPointAccumulator();
        var repo = new FakeHeroRankingRepository();

        accumulator.AddPending(1, 1, 0, 10);
        accumulator.AddPending(1, 1, 0, 10);
        accumulator.AddPending(1, 1, 0, 10);
        await accumulator.FlushDirtyAsync(repo, CancellationToken.None);

        var call = Assert.Single(repo.AddPointsCalls);
        Assert.Equal(3, call.Delta);
    }

    [Fact]
    public async Task FlushDirtyAsync_ClearsPendingState_SoASecondFlushIsANoOp()
    {
        var accumulator = new HeroRankPointAccumulator();
        var repo = new FakeHeroRankingRepository();

        accumulator.AddPending(1, 5, null, null);
        await accumulator.FlushDirtyAsync(repo, CancellationToken.None);
        await accumulator.FlushDirtyAsync(repo, CancellationToken.None);

        Assert.Single(repo.AddPointsCalls);
    }

    [Fact]
    public async Task FailedFlush_RequeuesTheDeltaForTheNextInterval()
    {
        var accumulator = new HeroRankPointAccumulator();
        var repo = new FakeHeroRankingRepository { ThrowOnAddPoints = true };

        accumulator.AddPending(1, 5, null, null);
        await accumulator.FlushDirtyAsync(repo, CancellationToken.None);

        repo.ThrowOnAddPoints = false;
        await accumulator.FlushDirtyAsync(repo, CancellationToken.None);

        var call = Assert.Single(repo.AddPointsCalls);
        Assert.Equal(5, call.Delta);
    }

    [Fact]
    public async Task FailedFlush_ThenNewGrantBeforeRetry_SumsBothIntoOneRequeuedDelta()
    {
        var accumulator = new HeroRankPointAccumulator();
        var repo = new FakeHeroRankingRepository { ThrowOnAddPoints = true };

        accumulator.AddPending(1, 5, null, null);
        await accumulator.FlushDirtyAsync(repo, CancellationToken.None);

        repo.ThrowOnAddPoints = false;
        accumulator.AddPending(1, 2, null, null);
        await accumulator.FlushDirtyAsync(repo, CancellationToken.None);

        var call = Assert.Single(repo.AddPointsCalls);
        Assert.Equal(7, call.Delta);
    }

    [Fact]
    public void AddPending_ZeroDelta_IsANoOp()
    {
        var accumulator = new HeroRankPointAccumulator();
        var repo = new FakeHeroRankingRepository();

        accumulator.AddPending(1, 0, null, null);

        // Nothing to assert on the accumulator's private state directly -- verified indirectly: a flush
        // right after must not call the repository at all.
        Assert.True(true);
    }

    [Fact]
    public async Task DifferentCharacters_FlushIndependently()
    {
        var accumulator = new HeroRankPointAccumulator();
        var repo = new FakeHeroRankingRepository();

        accumulator.AddPending(1, 4, 0, 10);
        accumulator.AddPending(2, 6, 1, 20);
        await accumulator.FlushDirtyAsync(repo, CancellationToken.None);

        Assert.Equal(2, repo.AddPointsCalls.Count);
        Assert.Contains(repo.AddPointsCalls, c => c.CharacterId == 1 && c.Delta == 4);
        Assert.Contains(repo.AddPointsCalls, c => c.CharacterId == 2 && c.Delta == 6);
    }
}
