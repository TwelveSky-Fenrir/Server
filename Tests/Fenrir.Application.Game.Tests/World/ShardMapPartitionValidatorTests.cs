using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.World;

public class ShardMapPartitionValidatorTests
{
    [Fact]
    public void DisjointPartition_ReportsNoConflicts()
    {
        ShardMapPartitionValidator.ShardMapClaim[] claims =
        [
            new(1, new short[] { 1, 2, 3 }),
            new(2, new short[] { 4, 5, 6 })
        ];

        var conflicts = ShardMapPartitionValidator.FindConflicts(claims);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void TwoShardsClaimingTheSameMap_IsDetectedAsAConflict()
    {
        ShardMapPartitionValidator.ShardMapClaim[] claims =
        [
            new(1, new short[] { 1, 2, 3 }),
            new(2, new short[] { 3, 4, 5 })
        ];

        var conflicts = ShardMapPartitionValidator.FindConflicts(claims);

        var conflict = Assert.Single(conflicts);
        Assert.Equal((byte)1, conflict.ShardIdA);
        Assert.Equal((byte)2, conflict.ShardIdB);
        Assert.Equal(new short[] { 3 }, conflict.MapIds);
    }

    [Fact]
    public void MultipleOverlappingMaps_AreAllCollectedUnderTheSamePair()
    {
        ShardMapPartitionValidator.ShardMapClaim[] claims =
        [
            new(5, new short[] { 10, 11, 12 }),
            new(3, new short[] { 11, 12, 13 })
        ];

        var conflicts = ShardMapPartitionValidator.FindConflicts(claims);

        var conflict = Assert.Single(conflicts);
        // Lower shard id always reported first, regardless of input order.
        Assert.Equal((byte)3, conflict.ShardIdA);
        Assert.Equal((byte)5, conflict.ShardIdB);
        Assert.Equal(new short[] { 11, 12 }, conflict.MapIds);
    }

    [Fact]
    public void SameShardListedTwice_IsNotReportedAsAConflictWithItself()
    {
        // e.g. a duplicate/self directory row observed at the boundary of a fast restart.
        ShardMapPartitionValidator.ShardMapClaim[] claims =
        [
            new(1, new short[] { 1, 2 }),
            new(1, new short[] { 2, 3 })
        ];

        var conflicts = ShardMapPartitionValidator.FindConflicts(claims);

        Assert.Empty(conflicts);
    }

    [Fact]
    public void ThreeShards_OnlyTheOverlappingPairIsReported()
    {
        ShardMapPartitionValidator.ShardMapClaim[] claims =
        [
            new(1, new short[] { 1, 2 }),
            new(2, new short[] { 3, 4 }),
            new(3, new short[] { 4, 5 })
        ];

        var conflicts = ShardMapPartitionValidator.FindConflicts(claims);

        var conflict = Assert.Single(conflicts);
        Assert.Equal((byte)2, conflict.ShardIdA);
        Assert.Equal((byte)3, conflict.ShardIdB);
        Assert.Equal(new short[] { 4 }, conflict.MapIds);
    }
}
