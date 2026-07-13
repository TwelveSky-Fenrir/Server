using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Directory;

public sealed class ZoneDirectory(
    IGameServerDirectoryRepository directory,
    IShardMapAssignmentRepository shardMapAssignments,
    ILogger<ZoneDirectory> logger) : IZoneDirectory
{
    public async ValueTask<ZoneEndpoint?> ResolveAsync(short zoneId, CancellationToken cancellationToken)
    {
        var shards = await directory.GetDirectoryAsync(cancellationToken).ConfigureAwait(false);

        var shard = await FindShardHostingMapAsync(shards, zoneId, cancellationToken).ConfigureAwait(false);
        if (shard is null)
        {
            if (shards.IsEmpty)
                logger.LogWarning(
                    "No live shard is registered in runtime.GameServerDirectory; cannot resolve an endpoint for zone {ZoneId}",
                    zoneId);
            else
                logger.LogWarning(
                    "Live shards exist but none of them claims zone (MapId) {ZoneId} in admin.ShardMapAssignments; cannot resolve its endpoint",
                    zoneId);
            return null;
        }

        // Modèle « zone = endpoint » (Décision A) : le port de la zone est DÉRIVÉ de son numéro de map (legacy
        // 1100 + N, ts25zone/S02_MyServer.cpp:15-16), PAS le port unique du shard. shard.Host reste l'hôte public
        // annoncé par le GameServer ; shard.Port n'est plus un port de jeu (informationnel).
        return new ZoneEndpoint(zoneId, shard.Host, LegacyZoneBasePort + zoneId);
    }

    // Base d'adressage legacy des zones (1100 + N). Cette amorce Cluster est dormante (le Login route via ses repos
    // + LoginServerOptions.ZoneBasePort) ; la constante garde le seam correct pour F4 quand le Center portera le
    // registre zone -> (host, port).
    private const int LegacyZoneBasePort = 1100;

        public ValueTask HeartbeatAsync(ZoneEndpoint endpoint, int currentPlayers, CancellationToken cancellationToken)
    {
        logger.LogDebug(
            "ZoneDirectory.HeartbeatAsync ignored for zone {ZoneId} ({Host}:{Port}, {CurrentPlayers} players): shard " +
            "presence is authored by the GameServer's own directory heartbeat, not by the passive CenterServer",
            endpoint.ZoneId, endpoint.Host, endpoint.Port, currentPlayers);
        return ValueTask.CompletedTask;
    }

    private async ValueTask<ShardDirectoryEntryDto?> FindShardHostingMapAsync(
        ImmutableArray<ShardDirectoryEntryDto> shards, short zoneId, CancellationToken cancellationToken)
    {
        foreach (var candidate in shards)
        {
            var hostedMaps = await shardMapAssignments.GetHostedMapsAsync(candidate.ShardId, cancellationToken)
                .ConfigureAwait(false);
            if (hostedMaps.Contains(zoneId))
                return candidate;
        }

        return null;
    }
}
