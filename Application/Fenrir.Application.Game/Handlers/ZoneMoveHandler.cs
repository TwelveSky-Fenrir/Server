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
///     op20, CZ_DEMAND_ZONE_SERVER_INFO_2 (contracts/01_replication_combat.md) -- the client-driven zone-transfer
///     request covering EVERY transfer reason the wire distinguishes only by <c>Sort</c> (2=GM, 3=death "return
///     to town", 4=portal, 5=paying NPC, 6=NPC move, 7=return, 8=return item, 9=teleport item, 10=NPC,
///     11=auto→zone 37, 12=zone 84): the legacy wire-level validation does not itself vary by reason beyond the
///     Sort==2/GM special case, so one handler covers all of them. Validation is exactly the legacy's own
///     (S04_MyWork02.cpp:2065-2114, reproduced in the contract's own XML doc on <see cref="ZoneMoveRequest" />)
///     and nothing more: per-reason business rules the legacy also enforces (portal trigger proximity &lt;30
///     units, the PvP anti-revive-hack restricting a dying player to zone 38 only, GM rank beyond "reject
///     Sort==2") are NOT modeled here -- an explicit V1 scope cut, not an oversight.
/// </summary>
/// <remarks>
///     ADR-0012: unlike the legacy (client disconnects, reconnects to the target zone's OWN process, replays
///     TEMP_REGISTER_SEND + CZ_REGISTER_AVATAR_SEND), Fenrir keeps the SAME TCP connection for an intra-shard
///     transfer -- there is only one process to reconnect to, and ADR-0012 explicitly chose not to reproduce the
///     reconnect dance. This handler therefore does, in one shot and on one connection, everything the legacy
///     split across two: resolve the destination, reply with THIS shard's own ip:port (the client is already
///     connected to it), push a fresh world-state snapshot mirroring <see cref="EnterWorldHandler" />'s
///     own three packets directly on this session, then hand the character off to the target zone actor.
///     <para>
///     HYPOTHESIS TO VALIDATE AGAINST A REAL CLIENT (flagged, not silently assumed): the legacy client, upon
///     receiving ip:port in <see cref="ZoneMoveResponse" />, may attempt its own disconnect/reconnect
///     regardless of whether ip:port name the socket it is already on. If so, this simplification breaks the
///     transfer flow and the handler needs revisiting (e.g. an actual reconnect-friendly path) -- see this
///     task's StructuredOutput openIssues.
///     </para>
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

        // Benign staleness window only (InWorld gating already guarantees CurrentZone was set at registration)
        // -- same defensive pattern as AvatarActionHandler/AvatarActionResumeHandler.
        if (zoneSession.CurrentZone is not Zone sourceZone)
            return ValueTask.CompletedTask;

        // "tZoneNumber == zone courante" -> silent ignore (contract doc, report 12 §4.2 point 1): no response
        // at all, matching the legacy's bare `return`.
        if (packet.ZoneNumber == sourceZone.MapId)
            return ValueTask.CompletedTask;

        // Combined range+consistency guard -- the same Quit()-worthy violation class as
        // EnterWorldHandler's anti-tamper checks (contract doc: tZoneNumber<1||>=350, or
        // tPresentZoneNumber not matching reality).
        if (packet.ZoneNumber is < 1 or >= 350 || packet.PresentZoneNumber != sourceZone.MapId)
        {
            zoneSession.Abort(DisconnectReason.Faulted);
            return ValueTask.CompletedTask;
        }

        // Sort==2 (GM transfer) requires uUserSort>=1 in the legacy -- Fenrir has no GM rank concept yet (Phase
        // C backlog), so this branch can never legitimately succeed for anyone: reproduce the same Quit() a
        // non-GM account gets in the legacy, rather than silently no-opping or crashing.
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

        // Not hosted by this shard -- Result=1, no transfer (ADR-0012 rule 4: cross-shard routing is future
        // work, out of scope while a single shard hosts every map).
        if (!zones.TryGet(targetZoneNumber, out var targetZone))
        {
            session.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
            return ValueTask.CompletedTask;
        }

        // Defensive, not expected in practice: a shard's Game:Maps could in principle name a map number this
        // build's world.Zones catalog never shipped (misconfiguration). Same "zone unavailable" fallback as an
        // unhosted target rather than an unhandled KeyNotFoundException.
        if (!worldData.ZonesByNumber.TryGetValue(targetZoneNumber, out var targetDefinition))
        {
            logger.LogError(
                "Zone {TargetZoneNumber} is hosted by this shard but absent from WorldDataCache -- refusing transfer for character {CharacterId}",
                targetZoneNumber, characterId);
            session.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
            return ValueTask.CompletedTask;
        }

        // Extremely narrow race: the source zone's own tick already removed this player (disconnect, another
        // handoff, ApplyDeath's own cross-zone revive) between the CurrentZone read above and this lookup.
        // Nothing to transfer -- the character isn't anywhere for this handler to act on.
        if (!sourceZone.TryGetPlayer(characterId, out var state) || state is null)
            return ValueTask.CompletedTask;

        // Arrival point (report 05 §7/§8): the slot recorded for players arriving FROM the zone they are
        // currently in, falling back to the target zone's own default spawn when no such slot exists -- the
        // legacy's own documented fallback (WorldDefinitions.ZoneDefinition.FindSpawnPointFrom's doc).
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

        // Fresh world-state snapshot on the SAME connection (ADR-0012, class remarks): the same three packets
        // EnterWorldHandler sends at initial world-entry, built from the live PlayerRuntimeState
        // (still valid here -- read above, BEFORE the handoff below removes it from sourceZone) rather than a
        // fresh SQL read, since nothing here needs data the zone doesn't already track in memory.
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

        // Self-spawn (mirrors EnterWorldHandler §5.9 step 3), reusing Zone's own packet builder with
        // an explicit idle ActionInfo at the RESOLVED arrival position -- state.PosX/Y/Z is still the SOURCE
        // zone's position at this point (single-writer invariant: only a zone's own tick may mutate
        // PlayerRuntimeState, and the actual position change happens later, inside sourceZone's tick, via the
        // Leave/Enter pair posted below).
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

        // ADR-0012: the live state travels INSIDE the Leave/Enter pair, position overridden to the resolved
        // arrival point (ZoneCommand.HandoffPosition / ZoneTransfer.CreateEnterData's own doc) -- posted to the
        // SOURCE zone, never mutated directly here (single-writer invariant, architecture reference §10.1).
        if (!sourceZone.Post(ZoneCommand.Leave(characterId, targetZone, (posX, posY, posZ))))
            logger.LogError(
                "Zone {SourceMapId} inbox full: dropped transfer Leave for character {CharacterId} to zone {TargetMapId}",
                sourceZone.MapId, characterId, targetZoneNumber);

        return ValueTask.CompletedTask;
    }
}
