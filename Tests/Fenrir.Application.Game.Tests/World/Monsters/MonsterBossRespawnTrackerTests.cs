using Fenrir.Application.Game.World.Monsters;
using Microsoft.Extensions.Logging.Abstractions;

namespace Fenrir.Application.Game.Tests.World.Monsters;

public class MonsterBossRespawnTrackerTests
{
    [Fact]
    public async Task InitializeAsync_LoadsEveryPersistedDeadline()
    {
        var repository = new FakeMonsterBossRespawnTimerRepository();
        var deadline = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        repository.Rows[42] = deadline;

        var tracker = new MonsterBossRespawnTracker(NullLogger<MonsterBossRespawnTracker>.Instance);
        await tracker.InitializeAsync(repository, CancellationToken.None);

        Assert.True(tracker.TryGetNextSpawnUtc(42, out var loaded));
        Assert.Equal(deadline, loaded);
    }

    [Fact]
    public async Task InitializeAsync_CalledTwice_Throws()
    {
        var tracker = new MonsterBossRespawnTracker(NullLogger<MonsterBossRespawnTracker>.Instance);
        await tracker.InitializeAsync(new FakeMonsterBossRespawnTimerRepository(), CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            tracker.InitializeAsync(new FakeMonsterBossRespawnTimerRepository(), CancellationToken.None));
    }

    [Fact]
    public async Task TryGetNextSpawnUtc_UnknownRegion_ReturnsFalse()
    {
        var tracker = new MonsterBossRespawnTracker(NullLogger<MonsterBossRespawnTracker>.Instance);
        await tracker.InitializeAsync(new FakeMonsterBossRespawnTimerRepository(), CancellationToken.None);

        Assert.False(tracker.TryGetNextSpawnUtc(999, out _));
    }

    [Fact]
    public async Task SetNextSpawnUtc_IsImmediatelyVisibleToTryGet_EvenBeforeAnyFlush()
    {
        var tracker = new MonsterBossRespawnTracker(NullLogger<MonsterBossRespawnTracker>.Instance);
        await tracker.InitializeAsync(new FakeMonsterBossRespawnTimerRepository(), CancellationToken.None);

        var deadline = DateTime.UtcNow.AddMinutes(30);
        tracker.SetNextSpawnUtc(7, deadline);

        Assert.True(tracker.TryGetNextSpawnUtc(7, out var loaded));
        Assert.Equal(deadline, loaded);
    }

    [Fact]
    public async Task FlushDirtyAsync_PersistsOnlyRegionsTouchedSinceTheLastFlush()
    {
        var repository = new FakeMonsterBossRespawnTimerRepository();
        var tracker = new MonsterBossRespawnTracker(NullLogger<MonsterBossRespawnTracker>.Instance);
        await tracker.InitializeAsync(repository, CancellationToken.None);

        var deadline = DateTime.UtcNow.AddHours(1);
        tracker.SetNextSpawnUtc(3, deadline);

        await tracker.FlushDirtyAsync(repository, CancellationToken.None);

        var call = Assert.Single(repository.SetCalls);
        Assert.Equal(3, call.MonsterSpawnRegionId);
        Assert.Equal(deadline, call.NextSpawnUtc);

        // Nothing dirty anymore -- a second flush must not re-persist the same region.
        await tracker.FlushDirtyAsync(repository, CancellationToken.None);
        Assert.Single(repository.SetCalls);
    }

    [Fact]
    public async Task FlushDirtyAsync_OnRepositoryFailure_LeavesTheRegionDirtyForTheNextInterval()
    {
        var repository = new FakeMonsterBossRespawnTimerRepository { ThrowOnSet = true };
        var tracker = new MonsterBossRespawnTracker(NullLogger<MonsterBossRespawnTracker>.Instance);
        await tracker.InitializeAsync(repository, CancellationToken.None);

        tracker.SetNextSpawnUtc(5, DateTime.UtcNow.AddMinutes(10));

        await tracker.FlushDirtyAsync(repository, CancellationToken.None); // swallows the simulated failure
        Assert.Empty(repository.SetCalls);

        repository.ThrowOnSet = false;
        await tracker.FlushDirtyAsync(repository, CancellationToken.None); // retried on the next interval

        Assert.Single(repository.SetCalls);
    }

    [Fact]
    public async Task FlushDirtyAsync_WithNothingDirty_NeverCallsTheRepository()
    {
        var repository = new FakeMonsterBossRespawnTimerRepository();
        var tracker = new MonsterBossRespawnTracker(NullLogger<MonsterBossRespawnTracker>.Instance);
        await tracker.InitializeAsync(repository, CancellationToken.None);

        await tracker.FlushDirtyAsync(repository, CancellationToken.None);

        Assert.Empty(repository.SetCalls);
    }
}
