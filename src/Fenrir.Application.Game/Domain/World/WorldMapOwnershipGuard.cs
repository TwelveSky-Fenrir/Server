namespace Fenrir.Application.Game.Domain.World;

public static class WorldMapOwnershipGuard
{
    public static void EnsureComplete(byte shardId, IReadOnlyCollection<short> hostedMaps,
        IReadOnlyCollection<short> worldMaps)
    {
        var hosted = new HashSet<short>(hostedMaps);
        var missing = worldMaps.Where(mapId => !hosted.Contains(mapId)).Order().ToArray();

        if (missing.Length == 0)
            return;

        throw new InvalidOperationException(
            $"GameServer {shardId} is configured as the sole world host, but it does not own {missing.Length} " +
            $"configured map(s): [{string.Join(", ", missing)}]. Assign every world.Zones.ZoneNumber to " +
            $"ShardId {shardId} in admin.ShardMapAssignments before starting this two-process topology.");
    }
}
