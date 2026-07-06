using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Avatars;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ZoneMoveService(
    ZoneRegistry zones,
    WorldDataCache worldData,
    GuildRankingCache guildRanking,
    WorldStateService worldState,
    IGameServerDirectoryRepository directory,
    IShardMapAssignmentRepository shardMapAssignments,
    ISessionTicketRepository tickets,
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

        // Not hosted by THIS shard's static ZoneRegistry -- legacy never distinguishes "local" from "remote"
        // here (Server/ts25zone/S04_MyWork02.cpp:2017-2186): every zone number is resolved live against the
        // same cross-process directory (Server/ts25center/S04_MyWork02.cpp:74-109's mZoneConnectionInfo), and
        // only a target whose own process isn't currently registered gets the port==0/Result=1 failure. Given
        // Fenrir's current shard/map coverage this "not local" case is the routine one, not the rare one, so
        // it must be resolved against the live runtime.GameServerDirectory + admin.ShardMapAssignments pair
        // before concluding the zone is genuinely unavailable -- see HandleCrossShardAsync.
        if (!zones.TryGet(targetZoneNumber, out var targetZone))
            return HandleCrossShardAsync(targetZoneNumber, characterId, zoneSession, cancellationToken);

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

        // mProtect_ReviveHack companion check (S04_MyWork02.cpp:2017-2064): a session still flagged from an
        // unresolved death is kicked outright on any zone-transfer attempt, unless the destination is zone 38
        // -- see ZoneTransferAntiAbuseRules' own remarks for why this alliance wording deliberately differs
        // from the tick-loop gate's.
        if (state.ReviveHackFlag &&
            !ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(sourceZone.MapId, targetZoneNumber, state.Tribe,
                worldState.GetAllyOf))
        {
            zoneSession.Abort(DisconnectReason.StateViolation);
            return ValueTask.CompletedTask;
        }

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

    /// <summary>
    ///     Live cross-process directory resolution for a zone number THIS shard's own <see cref="ZoneRegistry" />
    ///     does not host. Legacy draws no distinction at all between "hosted by the requester's own process" and
    ///     "hosted elsewhere": every target zone number is resolved against the same shared directory
    ///     (Server/ts25center/S04_MyWork02.cpp:74-109's <c>mZoneConnectionInfo</c>), and only a target whose own
    ///     process isn't currently registered gets the failure branch
    ///     (Server/ts25zone/S04_MyWork02.cpp:2143-2147, port==0). Fenrir's analog of that live directory is the
    ///     same <c>runtime.GameServerDirectory</c> + <c>admin.ShardMapAssignments</c> pair
    ///     <c>ZoneTransferService.ResolveShardForMapAsync</c> already reads for the Login-time equivalent (op22
    ///     <c>CL_DEMAND_ZONE_SERVER_INFO_SEND</c>) -- not a guess at a new mechanism.
    /// </summary>
    /// <remarks>
    ///     Mints a fresh, destination-shard-scoped, single-use <c>runtime.SessionTickets</c> row via
    ///     <see cref="ISessionTicketRepository.CreateAsync" /> before replying, so the destination shard's own
    ///     <c>ZoneHandshakeService.ConsumeTicketAsync</c> has a ticket to consume the instant the client
    ///     reconnects there and re-issues its ordinary ZoneHandshakeRequest with the same obfuscated account id
    ///     it always presents -- the identical mechanism Login's own <c>ZoneTransferService</c> already uses
    ///     for the first Login-&gt;Game handoff, just minted by GameServer itself here for a Game-&gt;Game
    ///     handoff (see this feature's own behavior contract). No local <see cref="ZoneCommand" /> is posted for
    ///     this player: the destination is a genuinely different TCP endpoint (unlike the same-connection
    ///     intra-shard handoff above, which ADR-0012 documents as a deliberate Fenrir simplification), so the
    ///     client is expected to disconnect this connection on its own -- the ordinary connection-close path
    ///     already flushes/tidies this player's in-memory state the same way any other disconnect does.
    /// </remarks>
    private async ValueTask HandleCrossShardAsync(short targetZoneNumber, int characterId,
        ZoneClientSession zoneSession, CancellationToken cancellationToken)
    {
        var shards = await directory.GetDirectoryAsync(cancellationToken);
        foreach (var candidate in shards)
        {
            // Already known absent from this shard's own ZoneRegistry -- skip the self-entry rather than
            // trusting a possibly-stale admin.ShardMapAssignments row that disagrees with what actually got
            // loaded at this shard's own boot (see ShardMapPartitionValidator's remarks on the same drift).
            if (candidate.ShardId == options.Value.ShardId)
                continue;

            var hostedMaps = await shardMapAssignments.GetHostedMapsAsync(candidate.ShardId, cancellationToken);
            if (!hostedMaps.Contains(targetZoneNumber))
                continue;

            // Account id/session token/account grade are already held by this connection since its own
            // handshake (ZoneClientSession.MarkTicketConsumed) -- never re-derived or re-queried here, same
            // posture as GenericActionHandler's own "AccountId is guaranteed set alongside CharacterId" note.
            // Without this mint, the destination shard's ZoneHandshakeService would find no
            // runtime.SessionTickets row at all once the client reconnects and would reject the handoff
            // (ZoneHandshakeOutcome.Rejected) instead of completing it.
            await tickets.CreateAsync(zoneSession.AccountId!.Value, characterId, candidate.ShardId,
                options.Value.TicketTtlSeconds, zoneSession.AccountSessionToken!.Value, zoneSession.AccountGrade,
                cancellationToken);

            logger.LogInformation(
                "Zone {TargetZoneNumber} resolved to shard {ShardId} ({Host}:{Port}) for character {CharacterId} -- cross-shard handoff ticket minted",
                targetZoneNumber, candidate.ShardId, candidate.Host, candidate.Port, characterId);
            zoneSession.Send(new ZoneMoveResponse { Result = 0, Ip = candidate.Host, Port = candidate.Port });
            return;
        }

        // No live shard (including this one) claims this zone number -- the legacy directory-sentinel branch:
        // that zone's own process genuinely isn't registered/running right now, which is the rare case, not
        // the routine one.
        logger.LogWarning(
            "Zone {TargetZoneNumber} is not hosted by any live shard -- refusing transfer for character {CharacterId}",
            targetZoneNumber, characterId);
        zoneSession.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
    }
}
