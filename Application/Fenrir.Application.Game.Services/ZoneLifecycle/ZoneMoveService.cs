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
    PortalProximityCatalog portalProximityCatalog,
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

        sourceZone.TryGetPlayer(characterId, out var state);

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

        if (TribeSymbolBattleZoneLockout.IsLockedOut(sourceZone.MapId, targetZoneNumber,
                worldState.World.TribeSymbolBattle))
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: tribe-symbol-battle zone lockout ({SourceMapId} -> {TargetZoneNumber})",
                characterId, sourceZone.MapId, targetZoneNumber);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return ValueTask.CompletedTask;
        }

        if (state is null)
        {
            logger.LogDebug(
                "Zone-move ignored for character {CharacterId}: no longer present in source zone {SourceMapId} (narrow race)",
                characterId, sourceZone.MapId);
            return ValueTask.CompletedTask;
        }

        if (PortalProximityGate.Evaluate(portalProximityCatalog, sourceZone.MapId, state.PosX, state.PosY,
                state.PosZ, packet.Sort, targetZoneNumber) == PortalProximityOutcome.RejectedNotNearRegisteredPortal)
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: portal move requested with no registered portal within range ({SourceMapId} -> {TargetZoneNumber})",
                characterId, sourceZone.MapId, targetZoneNumber);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return ValueTask.CompletedTask;
        }

        if (WarZoneEntryGate.Evaluate(targetZoneNumber, state.CombinedLevel, state.RebirthCount) ==
            WarZoneEntryOutcome.RejectedOutOfRange)
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: combined level {CombinedLevel}/rebirth {RebirthCount} out of range for war zone {TargetZoneNumber}",
                characterId, state.CombinedLevel, state.RebirthCount, targetZoneNumber);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return ValueTask.CompletedTask;
        }

        if (!zones.TryGet(targetZoneNumber, out var targetZone))
            return HandleCrossShardAsync(targetZoneNumber, characterId, state.Tribe, sourceZone.MapId,
                state.RebirthCount, zoneSession, cancellationToken);

        if (!worldData.ZonesByNumber.TryGetValue(targetZoneNumber, out var targetDefinition))
        {
            logger.LogError(
                "Zone {TargetZoneNumber} is hosted by this shard but absent from WorldDataCache -- refusing transfer for character {CharacterId}",
                targetZoneNumber, characterId);
            zoneSession.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
            return ValueTask.CompletedTask;
        }

        var corridorOutcome = WrapCheckSpecialDestinationGate.Evaluate(
            corridorCatalog,
            corridorState,
            state.Tribe,
            sourceZone.MapId,
            targetZoneNumber,
            zoneSession.IsGm,
            state.RebirthCount,
            worldState.World.Zone038WinTribe,
            worldState.GetAllyOf);

        switch (corridorOutcome)
        {
            case TribeGuardCorridorMoveOutcome.RejectedHardDisconnect:
                logger.LogWarning(
                    "Zone-move aborted for character {CharacterId}: tribe-guard corridor hard-disconnect ({SourceMapId} -> {TargetZoneNumber})",
                    characterId, sourceZone.MapId, targetZoneNumber);
                zoneSession.Abort(DisconnectReason.StateViolation);
                return ValueTask.CompletedTask;
            case TribeGuardCorridorMoveOutcome.RejectedSoft:
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

        if (!sourceZone.Post(ZoneCommand.Leave(characterId, targetZone, (posX, posY, posZ))))
            logger.LogError(
                "Zone {SourceMapId} inbox full: dropped transfer Leave for character {CharacterId} to zone {TargetMapId}",
                sourceZone.MapId, characterId, targetZoneNumber);

        return ValueTask.CompletedTask;
    }

        private async ValueTask HandleCrossShardAsync(short targetZoneNumber, int characterId, byte requesterTribe,
        short originZoneId, int requesterRebirthCount, ZoneClientSession zoneSession,
        CancellationToken cancellationToken)
    {
        var shards = await directory.GetDirectoryAsync(cancellationToken);
        foreach (var candidate in shards)
        {
            if (candidate.ShardId == options.Value.ShardId)
                continue;

            var hostedMaps = await shardMapAssignments.GetHostedMapsAsync(candidate.ShardId, cancellationToken);
            if (!hostedMaps.Contains(targetZoneNumber))
                continue;

            var corridorOutcome = WrapCheckSpecialDestinationGate.Evaluate(
                corridorCatalog,
                corridorState,
                requesterTribe,
                originZoneId,
                targetZoneNumber,
                zoneSession.IsGm,
                requesterRebirthCount,
                worldState.World.Zone038WinTribe,
                worldState.GetAllyOf);

            if (corridorOutcome == TribeGuardCorridorMoveOutcome.RejectedHardDisconnect)
            {
                logger.LogWarning(
                    "Zone-move aborted for character {CharacterId}: tribe-guard corridor hard-disconnect ({SourceMapId} -> {TargetZoneNumber}, cross-shard)",
                    characterId, originZoneId, targetZoneNumber);
                zoneSession.Abort(DisconnectReason.StateViolation);
                return;
            }

            if (corridorOutcome == TribeGuardCorridorMoveOutcome.RejectedSoft)
            {
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

            await tickets.CreateAsync(zoneSession.AccountId!.Value, characterId, candidate.ShardId,
                options.Value.TicketTtlSeconds, zoneSession.AccountSessionToken!.Value, zoneSession.AccountGrade,
                cancellationToken);

            zoneSession.MarkCrossShardTransferPending();

            logger.LogInformation(
                "Zone {TargetZoneNumber} resolved to shard {ShardId} ({Host}:{Port}) for character {CharacterId} -- cross-shard handoff ticket minted",
                targetZoneNumber, candidate.ShardId, candidate.Host, candidate.Port, characterId);

            if (zoneSession.CurrentZone is Zone sourceZone &&
                !sourceZone.Post(ZoneCommand.MarkZoneTransferPending(characterId)))
                logger.LogWarning(
                    "Zone {SourceMapId} inbox full: character {CharacterId}'s IsMovingZone flag was not set before its cross-shard handoff to zone {TargetZoneNumber}",
                    sourceZone.MapId, characterId, targetZoneNumber);

            zoneSession.Send(new ZoneMoveResponse { Result = 0, Ip = candidate.Host, Port = candidate.Port });
            return;
        }

        logger.LogWarning(
            "Zone {TargetZoneNumber} is not hosted by any live shard -- refusing transfer for character {CharacterId}",
            targetZoneNumber, characterId);
        zoneSession.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
    }
}
