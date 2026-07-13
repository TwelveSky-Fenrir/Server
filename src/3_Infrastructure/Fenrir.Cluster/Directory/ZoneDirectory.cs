using System.Collections.Immutable;
using Fenrir.Data.Abstractions.Admin;
using Fenrir.Data.Abstractions.Runtime;
using Microsoft.Extensions.Logging;

namespace Fenrir.Cluster.Directory;

/// <summary>
/// Implémentation réelle de <see cref="IZoneDirectory"/> adossée à l'annuaire <c>runtime.GameServerDirectory</c>
/// (via <see cref="IGameServerDirectoryRepository"/>) et à l'assignation carte→shard
/// (<see cref="IShardMapAssignmentRepository"/>). C'est le registre <c>zone → (host, port)</c> qu'un
/// enregistrement/redirection S2S interroge — successeur de la SHM <c>ZONE_CONNECTION_INFO</c> mono-hôte de
/// <c>ts25center</c> (<c>S04_MyWork02.cpp:105-108</c>), sans la contrainte mono-hôte.
/// </summary>
/// <remarks>
/// <para>
/// La résolution reproduit exactement le modèle déjà en place côté Login
/// (<c>ZoneTransferService.ResolveShardForMapAsync</c>) : une carte (zoneId) est hébergée par le shard dont
/// <see cref="IShardMapAssignmentRepository.GetHostedMapsAsync"/> la revendique, et le couple <c>(host, port)</c>
/// vient de la ligne d'annuaire vivante de ce shard. On ne réinvente pas le stockage : la vérité durable reste
/// en base (<c>runtime</c> In-Memory OLTP).
/// </para>
/// <para>
/// Pas de cache propre : <see cref="IGameServerDirectoryRepository.GetDirectoryAsync(CancellationToken)"/>
/// applique déjà un cache court (~2 s) côté repository, et l'assignation carte→shard est de la configuration
/// quasi-statique. Ajouter un second cache ici risquerait surtout de servir un endpoint périmé après un crash de
/// shard dans la fenêtre de fraîcheur.
/// </para>
/// </remarks>
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

        return new ZoneEndpoint(zoneId, shard.Host, shard.Port);
    }

    /// <summary>
    /// En Fenrir, la présence d'un shard est publiée par le GameServer lui-même (<c>GameServerDirectoryHeartbeat</c>
    /// écrit directement dans <c>runtime.GameServerDirectory</c>, scoping <c>shard</c> + capacité + p99 de tick que
    /// <see cref="ZoneEndpoint"/> ne porte pas). Le CenterServer n'est donc que le <b>lecteur</b> de cet annuaire :
    /// cette surface d'écriture reste un no-op tracé, à ne pas confondre avec le heartbeat autoritaire du shard.
    /// </summary>
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
