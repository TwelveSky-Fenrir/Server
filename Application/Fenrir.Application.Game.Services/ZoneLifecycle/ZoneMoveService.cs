using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Avatars;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Network.Dispatch.Zone.Sessions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Fenrir.Network.Serialization.Wire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ZoneMoveService(
    ZoneRegistry zones,
    WorldDataCache worldData,
    GuildRankingCache guildRanking,
    WorldStateService worldState,
    TribeGuardCorridorCatalog corridorCatalog,
    TribeGuardCorridorState corridorState,
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
        {
            logger.LogWarning(
                "Zone-move rejected for character {CharacterId}: session has no current zone",
                characterId);
            return ValueTask.CompletedTask;
        }

        var targetZoneNumber = (short)packet.ZoneNumber;

        // Resolved up front -- rather than immediately before the corridor gate further down, where this
        // lookup used to sit -- purely so the mProtect_ReviveHack companion check immediately below has the
        // live PlayerRuntimeState it needs. A miss here (narrow race: the source zone's own tick already
        // removed this player between the CurrentZone read above and this lookup) is handled further down,
        // once the malformed-input/sort/lockout gates that must still fire on their own terms regardless of
        // the race have had their say -- see that check's own comment below.
        sourceZone.TryGetPlayer(characterId, out var state);

        // mProtect_ReviveHack companion check (S04_MyWork02.cpp:2017-2064): runs UNCONDITIONALLY FIRST,
        // before the same-zone no-op short-circuit, the destination-range/present-zone validation, and the
        // sort-reason checks below -- legacy's own line ordering places this check (2019-2064) strictly
        // before the no-op check and everything that follows it (2065-2114); TribeSymbolBattleZoneLockout's
        // own remarks independently corroborate this same relative ordering from the other side. A session
        // still flagged from an unresolved death is kicked outright on ANY zone-transfer attempt --
        // including a same-zone no-op request, and including a request this shard would otherwise route
        // cross-shard (see HandleCrossShardAsync, reached further below only once this gate has already run)
        // -- unless the destination is zone 38. See ZoneTransferAntiAbuseRules' own remarks for why this
        // alliance wording deliberately differs from the tick-loop gate's. Skipped only when state is null
        // (the narrow-race miss above): there is nothing to protect in that case.
        if (state is not null && state.ReviveHackFlag &&
            !ZoneTransferAntiAbuseRules.AllowsTransferWhileFlagged(sourceZone.MapId, targetZoneNumber, state.Tribe,
                worldState.GetAllyOf))
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: revive-hack flag set, transfer {SourceMapId} -> {TargetZoneNumber} not allowed while flagged",
                characterId, sourceZone.MapId, targetZoneNumber);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return ValueTask.CompletedTask;
        }

        // Same zone requested -- silent ignore, matching the legacy's bare return (no response at all).
        if (packet.ZoneNumber == sourceZone.MapId)
        {
            logger.LogDebug(
                "Zone-move ignored for character {CharacterId}: target zone {TargetZoneNumber} is already the current zone",
                characterId, packet.ZoneNumber);
            return ValueTask.CompletedTask;
        }

        if (packet.ZoneNumber is < 1 or >= 350 || packet.PresentZoneNumber != sourceZone.MapId)
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: malformed target zone {TargetZoneNumber} or present-zone mismatch (claimed {ClaimedPresentZone}, actual {ActualPresentZone})",
                characterId, packet.ZoneNumber, packet.PresentZoneNumber, sourceZone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return ValueTask.CompletedTask;
        }

        // Sort==2 (GM transfer) requires operator rank, checked prior to and independent of the core
        // corridor predicate further below (Server/ts25zone/S04_MyWork02.cpp:2069-2075, gated on
        // tUserInfo->uUserSort >= 1 -- zoneSession.IsGm is the same uUserSort < 1 threshold
        // TribeGuardCorridorGate itself bypasses on further down). A non-operator session requesting sort 2
        // is rejected outright; an operator session falls through and is treated exactly like any other
        // recognized sort value for the rest of this handler.
        if (packet.Sort == 2 && !zoneSession.IsGm)
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: GM transfer (sort 2) requested without operator rank",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return ValueTask.CompletedTask;
        }

        if (packet.Sort is < 2 or > 12)
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: malformed sort {Sort}",
                characterId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return ValueTask.CompletedTask;
        }

        // Independent, unconditional tribe-symbol-battle zone-transfer lockout (Server/ts25zone/S04_MyWork02.cpp:
        // 2125-2141), evaluated here -- strictly BEFORE the connection-availability/cross-shard resolution just
        // below (zones.TryGet/HandleCrossShardAsync) -- because legacy itself evaluates this rule strictly
        // before its own port-availability check (Server/ts25zone/S04_MyWork02.cpp:2143-2166). A shard that does
        // not itself host zone 38 must never let this request slip through the cross-shard handoff path below
        // unchecked just because the local ZoneRegistry lookup would otherwise delegate away before this rule
        // ever ran -- that is exactly the leak this lockout exists to close. Needs only
        // sourceZone.MapId/targetZoneNumber/the world flag, so unlike the mProtect_ReviveHack check further down
        // it has no dependency on the live PlayerRuntimeState lookup: ANY player, not just one flagged by that
        // companion guard, moving from zone 40/41/42 into zone 38 while the tribe-symbol-battle event is open is
        // disconnected outright, with no response sent at all -- see TribeSymbolBattleZoneLockout's own remarks.
        if (TribeSymbolBattleZoneLockout.IsLockedOut(sourceZone.MapId, targetZoneNumber,
                worldState.World.TribeSymbolBattle))
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: tribe-symbol-battle zone lockout ({SourceMapId} -> {TargetZoneNumber})",
                characterId, sourceZone.MapId, targetZoneNumber);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return ValueTask.CompletedTask;
        }

        // Narrow race resolved: the TryGetPlayer lookup at the top of this method came back empty -- nothing
        // to transfer. Checked here, after the malformed-input/sort/tribe-symbol-battle gates above (which
        // must still fire on their own terms even under the race) but before every remaining gate below, all
        // of which need the live PlayerRuntimeState this race lost.
        if (state is null)
        {
            logger.LogDebug(
                "Zone-move ignored for character {CharacterId}: no longer present in source zone {SourceMapId} (narrow race)",
                characterId, sourceZone.MapId);
            return ValueTask.CompletedTask;
        }

        // Not hosted by THIS shard's static ZoneRegistry -- legacy never distinguishes "local" from "remote"
        // here (Server/ts25zone/S04_MyWork02.cpp:2017-2186): every zone number is resolved live against the
        // same cross-process directory (Server/ts25center/S04_MyWork02.cpp:74-109's mZoneConnectionInfo), and
        // only a target whose own process isn't currently registered gets the port==0/Result=1 failure. Given
        // Fenrir's current shard/map coverage this "not local" case is the routine one, not the rare one, so
        // it must be resolved against the live runtime.GameServerDirectory + admin.ShardMapAssignments pair
        // before concluding the zone is genuinely unavailable -- see HandleCrossShardAsync, which now also
        // runs the same TribeGuardCorridorGate check the same-shard path runs just below (a cross-shard
        // destination used to bypass the corridor gate entirely -- see HandleCrossShardAsync's own remarks).
        if (!zones.TryGet(targetZoneNumber, out var targetZone))
            return HandleCrossShardAsync(targetZoneNumber, characterId, state.Tribe, sourceZone.MapId, zoneSession,
                cancellationToken);

        if (!worldData.ZonesByNumber.TryGetValue(targetZoneNumber, out var targetDefinition))
        {
            logger.LogError(
                "Zone {TargetZoneNumber} is hosted by this shard but absent from WorldDataCache -- refusing transfer for character {CharacterId}",
                targetZoneNumber, characterId);
            zoneSession.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
            return ValueTask.CompletedTask;
        }

        // Tribe-guard corridor legality (MyUtil::WrapCheck, Server/ts25zone/S07_MyGame03.cpp:5721-5943), invoked
        // exactly where the legacy handler invokes it: after the mProtect_ReviveHack companion gate just above,
        // for every non-GM zone-transfer request regardless of reason (Server/ts25zone/S04_MyWork02.cpp:2143-2166,
        // gated on tUserInfo->uUserSort < 1 -- zoneSession.IsGm is the same uUserSort < 1 threshold). corridorCatalog
        // is registered as TribeGuardCorridorCatalog.Empty until the real sixteen-zone/hub table is populated (a
        // separate, not-yet-done data-gathering task -- see the catalog's own remarks), so this evaluates as a
        // documented always-allow for every destination today; enforcement activates automatically once that data
        // lands, with no further change to this handler required.
        var corridorOutcome = TribeGuardCorridorGate.Evaluate(
            corridorCatalog,
            corridorState,
            state.Tribe,
            sourceZone.MapId,
            targetZoneNumber,
            zoneSession.IsGm,
            worldState.GetAllyOf);

        switch (corridorOutcome)
        {
            case TribeGuardCorridorMoveOutcome.RejectedHardDisconnect:
                // Reserved zone 37 involved as either origin or destination -- hard failure, no response at all
                // (Server/ts25zone/S04_MyWork02.cpp:2152-2156).
                logger.LogWarning(
                    "Zone-move aborted for character {CharacterId}: tribe-guard corridor hard-disconnect ({SourceMapId} -> {TargetZoneNumber})",
                    characterId, sourceZone.MapId, targetZoneNumber);
                zoneSession.Abort(DisconnectReason.StateViolation);
                return ValueTask.CompletedTask;
            case TribeGuardCorridorMoveOutcome.RejectedSoft:
                // Routine corridor rejection -- a failure result immediately followed by the auto-zone fallback
                // redirect, the same B_RETURN_TO_AUTO_ZONE pairing ReturnToHomeZoneResponse already models for
                // AntiCampingForcedReturnSystem (Server/ts25zone/S05_MyTransfer.cpp:1166-1179). Legacy resolves
                // the destination's real ip/port BEFORE this gate ever runs and pairs the soft-failure result
                // with that already-resolved address rather than a blank one (Server/ts25zone/S04_MyWork02.cpp:
                // 2116-2118, 2159-2160) -- this corridor branch is only reached once zones.TryGet above has
                // already confirmed targetZoneNumber is hosted by THIS shard, so that resolved address is this
                // shard's own PublicHost/Port, identical to the success-path values sent a few lines below.
                logger.LogWarning(
                    "Zone-move redirected to auto-zone for character {CharacterId}: tribe-guard corridor rejected {SourceMapId} -> {TargetZoneNumber}",
                    characterId, sourceZone.MapId, targetZoneNumber);
                zoneSession.Send(new ZoneMoveResponse
                {
                    Result = 1,
                    Ip = options.Value.PublicHost,
                    Port = options.Value.Port
                });
                zoneSession.Send(new ReturnToHomeZoneResponse());
                return ValueTask.CompletedTask;
        }

        var spawnPoint = targetDefinition.FindSpawnPointFrom(sourceZone.MapId);
        var (posX, posY, posZ) = spawnPoint is null
            ? (targetDefinition.Zone.DefaultSpawnX, targetDefinition.Zone.DefaultSpawnY,
                targetDefinition.Zone.DefaultSpawnZ)
            : (spawnPoint.PosX, spawnPoint.PosY, spawnPoint.PosZ);

        logger.LogInformation(
            "Character {CharacterId} transferring same-shard: {SourceMapId} -> {TargetMapId} (sort {Sort})",
            characterId, sourceZone.MapId, targetZoneNumber, packet.Sort);

        zoneSession.Send(new ZoneMoveResponse
        {
            Result = 0,
            Ip = options.Value.PublicHost,
            Port = options.Value.Port
        });

        // Fresh world-state snapshot on the same connection, built from the still-valid PlayerRuntimeState
        // (read here, before the handoff below removes it from sourceZone) rather than a fresh SQL read.
        // BuffInfo mirrors the same carry-through-unless-zone-124 rule ZoneTransfer.CreateEnterData applies to
        // the actual handoff payload below (ZoneTransferBuffRules) -- state.Buffs is still the source zone's
        // live, accurate buff table at this point, not a stale/zeroed one (Server/ts25zone/S04_MyWork02.cpp:979
        // echoes the same retrieved buff state back to the client on an ordinary transfer).
        var registerRecv = new EnterWorldResponse
        {
            AvatarInfo = AvatarInfoFactory.CreateForRuntimeState(state, targetZoneNumber, posX, posY, posZ),
            BuffInfo = ZoneTransferBuffRules.Resolve(state.Buffs, targetZoneNumber)
        };
        zoneSession.Send(in registerRecv);

        var broadcastWorldInfo = new WorldSnapshotResponse
        {
            WorldInfo = GuildRankingProjection.Apply(WorldStateTemplates.ZeroedWorldInfo, guildRanking.Top),
            TribeInfo = WorldStateTemplates.ZeroedTribeInfo
        };
        zoneSession.Send(in broadcastWorldInfo);

        // state.PosX/Y/Z is still the SOURCE zone's position here (single-writer invariant: only a zone's own
        // tick may mutate PlayerRuntimeState); the actual move happens later via the Leave/Enter pair below.
        // Pet sub-fields read back from the source zone's own last-accepted CZ_UPDATE_PET_ACTION_SEND (op156)
        // state instead of the empty placeholder every ActionInfo builder used before the companion-pet-follow
        // wiring fix -- see PlayerRuntimeState.PetActionSort's own remarks.
        var selfSpawnAction = new ActionInfo
        {
            Type = 0,
            Sort = 0,
            Frame = 0,
            Location = [posX, posY, posZ],
            TargetLocation = [posX, posY, posZ],
            Front = state.Heading,
            TargetFront = state.Heading,
            PetLocation = [state.PetActionLocationX, state.PetActionLocationY, state.PetActionLocationZ],
            PetTargetLocation =
                [state.PetActionTargetLocationX, state.PetActionTargetLocationY, state.PetActionTargetLocationZ],
            PetFront = state.PetActionFront,
            PetSort = state.PetActionSort,
            TargetObjectSort = 0,
            TargetObjectIndex = 0,
            TargetObjectUniqueNumber = 0,
            SkillNumber = 0,
            SkillGradeNum1 = 0,
            SkillGradeNum2 = 0,
            SkillValue = 0
        };
        zoneSession.Send(sourceZone.BuildAvatarActionRecv(state, selfSpawnAction));

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
    ///     <para>
    ///         Fix: also runs <see cref="TribeGuardCorridorGate" /> once a live destination shard is found, for
    ///         the identical reason and against the identical <c>MyUtil::WrapCheck</c> switch
    ///         (Server/ts25zone/S07_MyGame03.cpp:5721-5943) as <see cref="HandleAsync" />'s own same-shard
    ///         evaluation just above -- legacy resolves every destination zone against the same single
    ///         connection-info table regardless of which process ultimately hosts it
    ///         (Server/ts25zone/S04_MyWork02.cpp:2017-2186), so a corridor zone assigned to a different shard
    ///         must be gated exactly like one this shard hosts itself, not silently exempted by
    ///         Fenrir's own shard-partitioning being an implementation detail legacy has no concept of. The
    ///         soft-rejection's paired ip/port uses <paramref name="candidate" />'s own resolved address (the
    ///         same value the ordinary success reply below sends), mirroring the same-shard branch's own
    ///         "already-resolved address, not a blank one" posture (Server/ts25zone/S04_MyWork02.cpp:2116-2118,
    ///         2159-2160).
    ///     </para>
    /// </remarks>
    private async ValueTask HandleCrossShardAsync(short targetZoneNumber, int characterId, byte requesterTribe,
        short originZoneId, ZoneClientSession zoneSession, CancellationToken cancellationToken)
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

            var corridorOutcome = TribeGuardCorridorGate.Evaluate(
                corridorCatalog,
                corridorState,
                requesterTribe,
                originZoneId,
                targetZoneNumber,
                zoneSession.IsGm,
                worldState.GetAllyOf);

            if (corridorOutcome == TribeGuardCorridorMoveOutcome.RejectedHardDisconnect)
            {
                // Reserved zone 37 involved as either origin or destination -- hard failure, no response at
                // all (Server/ts25zone/S04_MyWork02.cpp:2152-2156), identical to the same-shard branch.
                logger.LogWarning(
                    "Zone-move aborted for character {CharacterId}: tribe-guard corridor hard-disconnect ({SourceMapId} -> {TargetZoneNumber}, cross-shard)",
                    characterId, originZoneId, targetZoneNumber);
                zoneSession.Abort(DisconnectReason.StateViolation);
                return;
            }

            if (corridorOutcome == TribeGuardCorridorMoveOutcome.RejectedSoft)
            {
                // Same B_RETURN_TO_AUTO_ZONE pairing the same-shard branch sends, paired with the destination
                // shard's own already-resolved address rather than a blank one -- see this method's own
                // remarks. No ticket is minted and the player is never marked cross-shard-transfer-pending:
                // the request never actually leaves this shard.
                logger.LogWarning(
                    "Zone-move redirected to auto-zone for character {CharacterId}: tribe-guard corridor rejected {SourceMapId} -> {TargetZoneNumber} (cross-shard, destination shard {ShardId})",
                    characterId, originZoneId, targetZoneNumber, candidate.ShardId);
                zoneSession.Send(new ZoneMoveResponse
                {
                    Result = 1,
                    Ip = candidate.Host,
                    Port = candidate.Port
                });
                zoneSession.Send(new ReturnToHomeZoneResponse());
                return;
            }

            // Account id/session token/account grade are already held by this connection since its own
            // handshake (ZoneClientSession.MarkTicketConsumed) -- never re-derived or re-queried here, same
            // posture as GenericActionHandler's own "AccountId is guaranteed set alongside CharacterId" note.
            // Without this mint, the destination shard's ZoneHandshakeService would find no
            // runtime.SessionTickets row at all once the client reconnects and would reject the handoff
            // (ZoneHandshakeOutcome.Rejected) instead of completing it.
            await tickets.CreateAsync(zoneSession.AccountId!.Value, characterId, candidate.ShardId,
                options.Value.TicketTtlSeconds, zoneSession.AccountSessionToken!.Value, zoneSession.AccountGrade,
                cancellationToken);

            // Fix (cross-shard analog of the Login->Game HandoverIssued guard, see
            // GameConnectionHost.OnAcceptedAsync's own remarks): this connection is now guaranteed to close on
            // purpose, either on the client's own disconnect-then-reconnect-elsewhere or on a later abort --
            // mark it BEFORE the ZoneMoveResponse below is sent, so there is no window where the client could
            // already be reconnecting to the destination shard while this flag is still unset. Without this,
            // GameConnectionHost's own teardown races ahead of the destination shard's
            // ZoneHandshakeService.ConsumeTicketAsync -> TransitionToGameAsync claim and deletes the
            // runtime.AccountSessions row out from under it, turning a legitimate transfer into a spurious
            // ZoneHandshakeOutcome.SessionSuperseded disconnect.
            zoneSession.MarkCrossShardTransferPending();

            logger.LogInformation(
                "Zone {TargetZoneNumber} resolved to shard {ShardId} ({Host}:{Port}) for character {CharacterId} -- cross-shard handoff ticket minted",
                targetZoneNumber, candidate.ShardId, candidate.Host, candidate.Port, characterId);

            // Fenrir analog of legacy setting mMoveZoneResult=1 immediately before its own success reply
            // (Server/ts25zone/S04_MyWork02.cpp:2181-2185) -- see PlayerRuntimeState.IsMovingZone's own
            // remarks for why this matters specifically for the cross-shard path (this character stays live
            // and targetable in this shard's own zone for the whole real-world window until its actual
            // disconnect, unlike the same-shard handoff above). Routed through a ZoneCommand rather than
            // mutated directly: this method runs on a request thread, and PlayerRuntimeState may only be
            // mutated from the owning zone's own tick thread. Best-effort/fail-open: a full source-zone inbox
            // just means this character is briefly unguarded rather than blocking the handoff reply itself.
            if (zoneSession.CurrentZone is Zone sourceZone &&
                !sourceZone.Post(ZoneCommand.MarkZoneTransferPending(characterId)))
                logger.LogWarning(
                    "Zone {SourceMapId} inbox full: character {CharacterId}'s IsMovingZone flag was not set before its cross-shard handoff to zone {TargetZoneNumber}",
                    sourceZone.MapId, characterId, targetZoneNumber);

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
