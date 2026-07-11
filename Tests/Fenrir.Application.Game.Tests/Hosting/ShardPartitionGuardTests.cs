using Fenrir.Application.Game.Hosting;
using Fenrir.Application.Game.Tests.TestSupport;
using Fenrir.Data.Abstractions.Runtime;

namespace Fenrir.Application.Game.Tests.Hosting;

public class ShardPartitionGuardTests
{
    [Fact]
    public async Task SimultaneousColdBoot_EmptyLiveDirectory_StillDetectsOverlapFromTheStaticAssignmentTable()
    {
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
