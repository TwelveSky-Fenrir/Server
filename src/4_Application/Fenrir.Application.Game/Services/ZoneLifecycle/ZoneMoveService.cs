using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Avatars;
using Fenrir.Application.Game.Domain.Guilds;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.GameData;
using Fenrir.Data.Abstractions.Runtime;
using Fenrir.Network.Dispatch.Sessions;
using Fenrir.Application.Game;
using Fenrir.Core.Packets.Shared;
using Fenrir.Application.Game.Packets.Zone;
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
    ICharacterShardLocationRepository characterShardLocations,
    IEventLogRepository eventLog,
    IOptions<GameServerOptions> options,
    ILogger<ZoneMoveService> logger) : IZoneMoveService
{
    private const short ZoneDepartureEventCode = 5;

    public async ValueTask HandleAsync(ZoneMoveRequest packet, ZoneClientSession zoneSession,
        CancellationToken cancellationToken)
    {
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone sourceZone)
        {
            logger.LogWarning(
                "Zone-move rejected for character {CharacterId}: session has no current zone",
                characterId);
            return;
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
            return;
        }

        if (packet.ZoneNumber == sourceZone.MapId)
        {
            logger.LogDebug(
                "Zone-move ignored for character {CharacterId}: target zone {TargetZoneNumber} is already the current zone",
                characterId, packet.ZoneNumber);
            return;
        }

        if (!ZoneMoveDestinationZoneGate.IsWithinRequestRange(packet.ZoneNumber) ||
            packet.PresentZoneNumber != sourceZone.MapId)
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: malformed target zone {TargetZoneNumber} or present-zone mismatch (claimed {ClaimedPresentZone}, actual {ActualPresentZone})",
                characterId, packet.ZoneNumber, packet.PresentZoneNumber, sourceZone.MapId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (packet.Sort == (int)ZoneMoveActionCategory.GmMove && !zoneSession.IsGm)
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: GM transfer (sort 2) requested without operator rank",
                characterId);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (!ZoneMoveActionCategoryGate.IsRecognized(packet.Sort))
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: malformed sort {Sort}",
                characterId, packet.Sort);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        if (state is null)
        {
            logger.LogDebug(
                "Zone-move ignored for character {CharacterId}: no longer present in source zone {SourceMapId} (narrow race)",
                characterId, sourceZone.MapId);
            return;
        }

        if (TribeSymbolBattleZoneLockout.IsLockedOut(sourceZone.MapId, targetZoneNumber,
                worldState.World.TribeSymbolBattle))
        {
            logger.LogWarning(
                "Zone-move aborted for character {CharacterId}: tribe-symbol-battle zone lockout ({SourceMapId} -> {TargetZoneNumber})",
                characterId, sourceZone.MapId, targetZoneNumber);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        if (!zones.TryGet(targetZoneNumber, out var targetZone))
        {
            await HandleCrossShardAsync(targetZoneNumber, characterId, state, sourceZone.MapId, zoneSession,
                cancellationToken);
            return;
        }

        if (!worldData.ZonesByNumber.TryGetValue(targetZoneNumber, out var targetDefinition))
        {
            logger.LogError(
                "Zone {TargetZoneNumber} is hosted by this shard but absent from WorldDataCache -- refusing transfer for character {CharacterId}",
                targetZoneNumber, characterId);
            zoneSession.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
            return;
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
                return;
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
                return;
        }

        if (!sourceZone.Post(ZoneCommand.MarkZoneTransferPending(characterId)))
        {
            logger.LogError(
                "Zone {SourceMapId} inbox full: character {CharacterId}'s pending-transfer marker could not be queued before its handoff to zone {TargetZoneNumber} -- aborting session",
                sourceZone.MapId, characterId, targetZoneNumber);
            zoneSession.Abort(DisconnectReason.Faulted);
            return;
        }

        await LogZoneDepartureAsync(zoneSession, characterId, sourceZone.MapId, targetZoneNumber,
            cancellationToken);

        var spawnPoint = targetDefinition.FindSpawnPointFrom(sourceZone.MapId);
        var (posX, posY, posZ) = spawnPoint is null
            ? (targetDefinition.Zone.DefaultSpawnX, targetDefinition.Zone.DefaultSpawnY,
                targetDefinition.Zone.DefaultSpawnZ)
            : (spawnPoint.PosX, spawnPoint.PosY, spawnPoint.PosZ);

        logger.LogInformation(
            "Character {CharacterId} transferring same-shard: {SourceMapId} -> {TargetMapId} (sort {Sort})",
            characterId, sourceZone.MapId, targetZoneNumber, packet.Sort);

        await characterShardLocations.UpsertAsync(characterId, options.Value.ShardId, targetZoneNumber, state.Name,
            state.Tribe, cancellationToken);

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
    }

    private async ValueTask HandleCrossShardAsync(short targetZoneNumber, int characterId, PlayerRuntimeState state,
        short originZoneId, ZoneClientSession zoneSession, CancellationToken cancellationToken)
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
                state.Tribe,
                originZoneId,
                targetZoneNumber,
                zoneSession.IsGm,
                state.RebirthCount,
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

            ZoneTransferBuffRules.ClearIfDestinationRequiresIt(state.Buffs, targetZoneNumber);

            if (zoneSession.CurrentZone is not Zone sourceZone)
            {
                logger.LogError(
                    "Character {CharacterId}'s cross-shard handoff to zone {TargetZoneNumber} aborted: session has no current zone",
                    characterId, targetZoneNumber);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            await tickets.CreateAsync(zoneSession.AccountId!.Value, characterId, candidate.ShardId,
                options.Value.TicketTtlSeconds, zoneSession.AccountSessionToken!.Value, zoneSession.AccountGrade,
                cancellationToken);

            if (!sourceZone.Post(ZoneCommand.MarkZoneTransferPending(characterId)))
            {
                logger.LogError(
                    "Zone {SourceMapId} inbox full: character {CharacterId}'s pending-transfer marker could not be queued after its cross-shard handoff ticket was minted for zone {TargetZoneNumber} -- aborting session",
                    sourceZone.MapId, characterId, targetZoneNumber);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            zoneSession.MarkCrossShardTransferPending();

            logger.LogInformation(
                "Zone {TargetZoneNumber} resolved to shard {ShardId} ({Host}:{Port}) for character {CharacterId} -- cross-shard handoff ticket minted",
                targetZoneNumber, candidate.ShardId, candidate.Host, candidate.Port, characterId);

            await LogZoneDepartureAsync(zoneSession, characterId, originZoneId, targetZoneNumber, cancellationToken);

            await characterShardLocations.UpsertAsync(characterId, candidate.ShardId, targetZoneNumber, state.Name,
                state.Tribe, cancellationToken);

            zoneSession.Send(new ZoneMoveResponse { Result = 0, Ip = candidate.Host, Port = candidate.Port });
            return;
        }

        logger.LogWarning(
            "Zone {TargetZoneNumber} is not hosted by any live shard -- refusing transfer for character {CharacterId}",
            targetZoneNumber, characterId);
        zoneSession.Send(new ZoneMoveResponse { Result = 1, Ip = "", Port = 0 });
    }

    private async ValueTask LogZoneDepartureAsync(ZoneClientSession zoneSession, int characterId, short sourceMapId,
        short targetMapId, CancellationToken cancellationToken)
    {
        try
        {
            await eventLog.LogAsync(ZoneDepartureEventCode, EventLogCategory.Session, zoneSession.AccountId,
                characterId, null, null, options.Value.ShardId, null, null, null, null, 1,
                $"SourceMapId={sourceMapId},TargetMapId={targetMapId}", cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex,
                "Failed to write game.EventLog row for zone departure (character {CharacterId}, {SourceMapId} -> {TargetMapId})",
                characterId, sourceMapId, targetMapId);
        }
    }
}
