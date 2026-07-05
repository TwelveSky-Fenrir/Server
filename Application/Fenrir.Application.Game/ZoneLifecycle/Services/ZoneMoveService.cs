using Fenrir.Application.Game.Avatars;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.Guilds;
using Fenrir.Application.Game.World;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.ZoneLifecycle.Services;

/// <summary>
///     Business logic for op20, CZ_DEMAND_ZONE_SERVER_INFO_2 -- covers every zone-transfer reason the wire
///     distinguishes only by Sort (GM, death return, portal, paying NPC, teleport item, etc); see
///     <c>ZoneMoveHandler</c>'s own remarks for the full rationale, including ADR-0012's same-connection
///     intra-shard handoff design. Owns every send/abort/zone-command-post itself (rather than returning a
///     Result for the handler to translate) because success and failure are both threaded through several
///     interleaved, order-dependent session sends -- collapsing that into a single uniform result shape would
///     restructure control flow rather than merely relocate it.
/// </summary>
public interface IZoneMoveService
{
    ValueTask HandleAsync(ZoneMoveRequest packet, ZoneClientSession zoneSession, CancellationToken cancellationToken);
}

public sealed class ZoneMoveService(
    ZoneRegistry zones,
    WorldDataCache worldData,
    GuildRankingCache guildRanking,
    IOptions<GameServerOptions> options,
    ILogger<ZoneMoveService> logger) : IZoneMoveService
{
    public ValueTask HandleAsync(ZoneMoveRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken)
    {
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone sourceZone)
            return ValueTask.CompletedTask;

        // Same zone requested -- silent ignore, matching the legacy's bare return (no response at all).
        if (packet.ZoneNumber == sourceZone.MapId)
            return ValueTask.CompletedTask;

        if (packet.ZoneNumber is < 1 or >= 350 || packet.PresentZoneNumber != sourceZone.MapId)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return ValueTask.CompletedTask;
        }

        // Sort==2 (GM transfer) requires GM rank, which Fenrir has no concept of yet -- always rejected.
        if (packet.Sort == 2)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return ValueTask.CompletedTask;
        }

        if (packet.Sort is < 2 or > 12)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return ValueTask.CompletedTask;
        }

        var targetZoneNumber = (short)packet.ZoneNumber;

        // Not hosted by this shard -- cross-shard routing is future work.
        if (!zones.TryGet(targetZoneNumber, out var targetZone))
        {
            zoneSession.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
            return ValueTask.CompletedTask;
        }

        if (!worldData.ZonesByNumber.TryGetValue(targetZoneNumber, out var targetDefinition))
        {
            logger.LogError(
                "Zone {TargetZoneNumber} is hosted by this shard but absent from WorldDataCache -- refusing transfer for character {CharacterId}",
                targetZoneNumber, characterId);
            zoneSession.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
            return ValueTask.CompletedTask;
        }

        // Narrow race: the source zone's own tick already removed this player between the CurrentZone read
        // above and this lookup (disconnect/another handoff) -- nothing to transfer.
        if (!sourceZone.TryGetPlayer(characterId, out var state) || state is null)
            return ValueTask.CompletedTask;

        var spawnPoint = targetDefinition.FindSpawnPointFrom(sourceZone.MapId);
        var (posX, posY, posZ) = spawnPoint is null
            ? (targetDefinition.Zone.DefaultSpawnX, targetDefinition.Zone.DefaultSpawnY,
                targetDefinition.Zone.DefaultSpawnZ)
            : (spawnPoint.PosX, spawnPoint.PosY, spawnPoint.PosZ);

        zoneSession.Send(new ZoneMoveResponse
        {
            Result = 0,
            Ip = options.Value.PublicHost,
            Port = options.Value.Port
        });

        // Fresh world-state snapshot on the same connection, built from the still-valid PlayerRuntimeState
        // (read here, before the handoff below removes it from sourceZone) rather than a fresh SQL read.
        var registerRecv = new EnterWorldResponse
        {
            AvatarInfo = AvatarInfoFactory.CreateForRuntimeState(state, targetZoneNumber, posX, posY, posZ),
            BuffInfo = WorldStateTemplates.ZeroedBuffInfo
        };
        zoneSession.SendRaw(ZoneMessageFactory.Encode(in registerRecv));

        var broadcastWorldInfo = new WorldSnapshotResponse
        {
            WorldInfo = GuildRankingProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, guildRanking.Top),
            TribeInfo = WorldStateTemplates.ZeroedTribeInfo
        };
        zoneSession.SendRaw(ZoneMessageFactory.Encode(in broadcastWorldInfo));

        // state.PosX/Y/Z is still the SOURCE zone's position here (single-writer invariant: only a zone's own
        // tick may mutate PlayerRuntimeState); the actual move happens later via the Leave/Enter pair below.
        var selfSpawnAction = new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = [posX, posY, posZ],
            TargetLocation = [posX, posY, posZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = new float[3],
            PetTargetLocation = new float[3],
            PetFront = 0,
            PetSort = 0,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };
        zoneSession.Send(Zone.BuildAvatarActionRecv(state, selfSpawnAction));

        // Live state travels inside the Leave/Enter pair, position overridden to the resolved arrival point --
        // posted to the source zone, never mutated directly here (single-writer invariant).
        if (!sourceZone.Post(ZoneCommand.Leave(characterId, targetZone, (posX, posY, posZ))))
            logger.LogError(
                "Zone {SourceMapId} inbox full: dropped transfer Leave for character {CharacterId} to zone {TargetMapId}",
                sourceZone.MapId, characterId, targetZoneNumber);

        return ValueTask.CompletedTask;
    }
}
