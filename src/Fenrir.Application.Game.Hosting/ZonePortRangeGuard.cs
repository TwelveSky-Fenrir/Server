using System.Globalization;

namespace Fenrir.Application.Game.Hosting;

/// <remarks>
/// L'orchestrateur reserve un BLOC de ports pour les listeners de zone et ne peut pas connaitre la liste
/// des maps : elle vient de admin.ShardMapAssignments, resolue par le shard au boot. Ce garde ferme
/// l'ecart dans l'autre sens — il verifie que chaque port derive tombe bien dans le bloc annonce, plutot
/// que de dupliquer une liste de maps que SQL calcule deja (027_shard_map_assignments_all_zones.sql
/// derive l'assignation de world.Zones ; toute liste ecrite a la main divergerait au premier seed).
/// </remarks>
public static class ZonePortRangeGuard
{
    public static void EnsureAllPortsWithinReservedBlock(
        byte shardId,
        IReadOnlyCollection<short> hostedMaps,
        int zoneBasePort,
        int reservedRangeStart,
        int reservedRangeEnd,
        IReadOnlyCollection<int> portsReservedByOtherResources)
    {
        if (reservedRangeStart > reservedRangeEnd)
            throw new InvalidOperationException(
                $"Shard {shardId} cannot start: the declared zone port block is inverted " +
                $"({reservedRangeStart}..{reservedRangeEnd}). Fix Game__ZonePortRangeStart/End in the " +
                "orchestrator before anything else -- every check below depends on it.");

        var outOfBlock = new List<string>();
        var collisions = new List<string>();

        foreach (var mapId in hostedMaps.OrderBy(m => m))
        {
            var port = zoneBasePort + mapId;

            if (port < reservedRangeStart || port > reservedRangeEnd)
                outOfBlock.Add(
                    string.Format(CultureInfo.InvariantCulture, "map {0} -> port {1}", mapId, port));

            if (portsReservedByOtherResources.Contains(port))
                collisions.Add(
                    string.Format(CultureInfo.InvariantCulture, "map {0} -> port {1}", mapId, port));
        }

        if (outOfBlock.Count == 0 && collisions.Count == 0)
            return;

        var detail = new List<string>();

        if (outOfBlock.Count > 0)
            detail.Add(
                $"{outOfBlock.Count} hosted map(s) derive a port outside the reserved block " +
                $"{reservedRangeStart}..{reservedRangeEnd}: {string.Join(", ", outOfBlock)}");

        if (collisions.Count > 0)
            detail.Add(
                $"{collisions.Count} hosted map(s) derive a port already claimed by another orchestrated " +
                $"resource: {string.Join(", ", collisions)}");

        throw new InvalidOperationException(
            $"Shard {shardId} cannot start: {string.Join("; ", detail)}. The client's addressing plan is " +
            $"ZoneBasePort + mapId and is frozen by the legacy client, so the port cannot be moved -- either " +
            "widen the block the orchestrator reserves (Game__ZonePortRangeStart/End, and the matching " +
            "constant in AppHost.cs), or give the offending map a ZoneNumber inside the block. Starting " +
            "anyway would bind a port nobody declared: invisible to the dashboard, unprotected against " +
            "collision, and absent from any publish manifest.");
    }
}
