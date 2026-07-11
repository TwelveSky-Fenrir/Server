namespace Fenrir.Application.Game.Domain.World;

public static class ShardMapPartitionValidator
{

        public static IReadOnlyList<ShardMapConflict> FindConflicts(IReadOnlyList<ShardMapClaim> claims)
    {
        var claimedBy = new Dictionary<short, byte>();
        var overlapsByPair = new Dictionary<(byte Lower, byte Higher), List<short>>();

        foreach (var claim in claims)
        foreach (var mapId in claim.MapIds)
        {
            if (!claimedBy.TryGetValue(mapId, out var firstShardId))
            {
                claimedBy[mapId] = claim.ShardId;
                continue;
            }

            if (firstShardId == claim.ShardId)
                continue;

            var pair = firstShardId < claim.ShardId
                ? (firstShardId, claim.ShardId)
                : (claim.ShardId, firstShardId);

            if (!overlapsByPair.TryGetValue(pair, out var mapIds))
                overlapsByPair[pair] = mapIds = [];

            if (!mapIds.Contains(mapId))
                mapIds.Add(mapId);
        }

        return overlapsByPair
            .Select(entry => new ShardMapConflict(entry.Key.Lower, entry.Key.Higher, entry.Value))
            .ToArray();
    }

        public readonly record struct ShardMapClaim(byte ShardId, IReadOnlyCollection<short> MapIds);

        public readonly record struct ShardMapConflict(byte ShardIdA, byte ShardIdB, IReadOnlyList<short> MapIds);
}
