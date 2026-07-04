using Fenrir.Application.Game.Avatars;
using Fenrir.Application.Game.GameData;
using Fenrir.Application.Game.World;
using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Handlers;

/// <summary>
///     op20, CZ_DEMAND_ZONE_SERVER_INFO_2 -- covers every zone-transfer reason the wire distinguishes only by
///     Sort (GM, death return, portal, paying NPC, teleport item, etc); wire-level validation doesn't vary by
///     reason beyond the Sort==2/GM case, so one handler covers all of them. Per-reason business rules the
///     legacy also enforces (portal proximity, PvP anti-revive-hack, GM rank) are an explicit scope cut.
/// </summary>
/// <remarks>
///     ADR-0012: unlike the legacy (client disconnects and reconnects to the target zone's own process), Fenrir
///     keeps the same TCP connection for an intra-shard transfer -- this handler resolves the destination,
///     replies with this shard's own ip:port, pushes a fresh world-state snapshot on this session, then hands
///     the character to the target zone actor, all in one shot.
///     Unverified against a real client: it may attempt its own reconnect upon receiving ip:port regardless of
///     whether it already names the current socket -- if so this simplification needs revisiting.
/// </remarks>
public sealed class ZoneMoveHandler(
    ZoneRegistry zones,
    WorldDataCache worldData,
    IOptions<GameServerOptions> options,
    ILogger<ZoneMoveHandler> logger) : IAsyncPacketHandler<ZoneMoveRequest>
{
    public ValueTask HandleAsync(ZoneMoveRequest packet, IPacketSession session,
        CancellationToken cancellationToken)
    {
        var zoneSession = (ZoneClientSession)session;
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
            session.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
            return ValueTask.CompletedTask;
        }

        if (!worldData.ZonesByNumber.TryGetValue(targetZoneNumber, out var targetDefinition))
        {
            logger.LogError(
                "Zone {TargetZoneNumber} is hosted by this shard but absent from WorldDataCache -- refusing transfer for character {CharacterId}",
                targetZoneNumber, characterId);
            session.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
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

        session.Send(new ZoneMoveResponse
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
            WorldInfo = WorldStateTemplates.ZeroedWorldInfo,
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
