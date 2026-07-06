using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.Hosting;

// gameserver-directory-heartbeat-liveness: ShardPartitionGuard.EnsureNoOverlapAsync used to build its
// conflict-claim list solely from runtime.GameServerDirectory (who has already heartbeated). On a whole
// fleet cold-booting at the same instant -- the normal case, since every shard's own heartbeat only starts
// after its own guard check passes -- the live directory is empty for every shard's check simultaneously,
// so a genuine map overlap between two never-yet-live shards passed through undetected. These tests pin
// the fix: IShardMapAssignmentRepository.GetAllAssignmentsAsync reads admin.ShardMapAssignments directly,
// independent of liveness, closing that gap.
public class ShardPartitionGuardTests
{
    [Fact]
    public async Task SimultaneousColdBoot_EmptyLiveDirectory_StillDetectsOverlapFromTheStaticAssignmentTable()
    {
        // Neither shard has ever heartbeated yet -- the exact scenario the guard used to miss.
        var directory = new FakeGameServerDirectoryRepository();
        var shardMaps = new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>
        {
            [1] = [1, 6],
            [2] = [6, 11]
        });

        var ex = await Record.ExceptionAsync(() => ShardPartitionGuard.EnsureNoOverlapAsync(
            1, [1, 6], directory, shardMaps, CancellationToken.None));

        var thrown = Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("shard 2", thrown.Message);
        Assert.Contains("[6]", thrown.Message);
    }

    [Fact]
    public async Task SimultaneousColdBoot_DisjointStaticAssignments_NoConflictReported()
    {
        var directory = new FakeGameServerDirectoryRepository();
        var shardMaps = new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>
        {
            [1] = [1],
            [2] = [6, 11, 140]
        });

        var ex = await Record.ExceptionAsync(() => ShardPartitionGuard.EnsureNoOverlapAsync(
            1, [1], directory, shardMaps, CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task AlreadyLiveConflictingShard_IsStillDetected()
    {
        // Regression guard for the pre-existing, already-working case: the other shard has already
        // heartbeated, so the live-directory crossing alone would have caught this even before the fix.
        var otherShard = new ShardDirectoryEntryDto(2, "10.0.0.2", 30002, 0, 100, 0f);
        var directory = new FakeGameServerDirectoryRepository(otherShard);
        var shardMaps = new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>
        {
            [1] = [1, 6],
            [2] = [6, 11]
        });

        var ex = await Record.ExceptionAsync(() => ShardPartitionGuard.EnsureNoOverlapAsync(
            1, [1, 6], directory, shardMaps, CancellationToken.None));

        var thrown = Assert.IsType<InvalidOperationException>(ex);
        Assert.Contains("shard 2", thrown.Message);
    }

    [Fact]
    public async Task FastRestartSeeingOwnPriorHeartbeat_IsNotMistakenForASelfConflict()
    {
        var selfEntry = new ShardDirectoryEntryDto(1, "10.0.0.1", 30001, 0, 100, 0f);
        var directory = new FakeGameServerDirectoryRepository(selfEntry);
        var shardMaps = new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>
        {
            [1] = [1, 6]
        });

        var ex = await Record.ExceptionAsync(() => ShardPartitionGuard.EnsureNoOverlapAsync(
            1, [1, 6], directory, shardMaps, CancellationToken.None));

        Assert.Null(ex);
    }

    [Fact]
    public async Task ConflictBetweenTwoOtherShards_IsNotThisShardsConcern()
    {
        // Shards 2 and 3 collide with each other; shard 1 has no overlap with either -- not this boot's problem.
        var directory = new FakeGameServerDirectoryRepository();
        var shardMaps = new FakeShardMapAssignmentRepository(new Dictionary<byte, short[]>
        {
            [1] = [1],
            [2] = [6, 11],
            [3] = [11, 140]
        });

        var ex = await Record.ExceptionAsync(() => ShardPartitionGuard.EnsureNoOverlapAsync(
            1, [1], directory, shardMaps, CancellationToken.None));

        Assert.Null(ex);
    }
}
