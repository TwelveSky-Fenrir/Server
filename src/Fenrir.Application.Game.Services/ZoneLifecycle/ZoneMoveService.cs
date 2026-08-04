using System.Net;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.World;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Application.Game.Domain.World.WorldState;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Domain.Game.GameData;
using Fenrir.Protocol.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ZoneMoveService(
    ZoneRegistry zones,
    WorldDataCache worldData,
    WorldStateService worldState,
    TribeGuardCorridorCatalog corridorCatalog,
    TribeGuardCorridorState corridorState,
    IGameServerDirectoryRepository directory,
    IShardMapAssignmentRepository shardMapAssignments,
    IShardReachabilityProbe reachabilityProbe,
    ISessionTicketRepository tickets,
    ICharacterShardLocationRepository characterShardLocations,
    IEventLogRepository eventLog,
    IOptions<GameServerOptions> options,
    ICharacterWriteBehindFlusher writeBehindFlusher,
    ILogger<ZoneMoveService> logger) : IZoneMoveService
{
    private const short ZoneDepartureEventCode = 5;

    private static readonly TimeSpan ZoneActorCommandCompletionTimeout = TimeSpan.FromSeconds(2);

    public async ValueTask HandleAsync(ZoneMoveRequest packet, IZoneSession zoneSession,
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

        if (state.IsDead || state.Life <= 0)
        {
            logger.LogInformation(
                "Zone-move rejected for character {CharacterId}: a dead character cannot transfer before a validated resurrection",
                characterId);
            zoneSession.Send(RejectedZoneMoveResponse());
            return;
        }

        if (state.IsMovingZone)
        {
            logger.LogDebug(
                "Zone-move ignored for character {CharacterId}: a zone transfer is already pending",
                characterId);
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

        if (!zones.TryGet(targetZoneNumber, out _))
        {
            await HandleCrossShardAsync(targetZoneNumber, characterId, state, sourceZone.MapId, zoneSession,
                cancellationToken);
            return;
        }


        if (!worldData.ZonesByNumber.ContainsKey(targetZoneNumber))
        {
            logger.LogError(
                "Zone {TargetZoneNumber} is hosted by this shard but absent from WorldDataCache -- refusing transfer for character {CharacterId}",
                targetZoneNumber, characterId);
            zoneSession.Send(RejectedZoneMoveResponse());
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
                    Port = options.Value.ZoneBasePort + targetZoneNumber
                });
                zoneSession.Send(new ReturnToHomeZoneResponse());
                return;
        }

        if (zoneSession.RemoteEndPoint?.Address is not { } sourceAddress)
        {
            logger.LogWarning(
                "Zone-move rejected for character {CharacterId}: the source address is unavailable, so its handoff capability cannot be IP-bound",
                characterId);
            zoneSession.Send(RejectedZoneMoveResponse());
            return;
        }

        var begin = await BeginHandoffAsync(sourceZone, characterId, targetZoneNumber, cancellationToken)
            .ConfigureAwait(false);
        if (begin.Kind != HandoffBeginOutcomeKind.Applied)
        {
            HandleUnsuccessfulHandoffBegin(zoneSession, characterId, sourceZone.MapId, targetZoneNumber, begin.Kind);
            return;
        }

        var handoffCompleted = await CompleteHandoffAsync(sourceZone, zoneSession, characterId, sourceZone.MapId,
                options.Value.ShardId, targetZoneNumber, sourceAddress, begin.Snapshot!, cancellationToken)
            .ConfigureAwait(false);
        if (!handoffCompleted)
        {
            zoneSession.Send(RejectedZoneMoveResponse());
            return;
        }

        logger.LogInformation(
            "Character {CharacterId} transferring same-shard: {SourceMapId} -> {TargetMapId} (sort {Sort}) -- handoff ticket minted, awaiting reconnection",
            characterId, sourceZone.MapId, targetZoneNumber, packet.Sort);

        await LogZoneDepartureAsync(zoneSession, characterId, sourceZone.MapId, targetZoneNumber,
            cancellationToken);

        zoneSession.Send(new ZoneMoveResponse
        {
            Result = 0,
            Ip = options.Value.PublicHost,
            Port = options.Value.ZoneBasePort + targetZoneNumber
        });
    }

    private async ValueTask HandleCrossShardAsync(short targetZoneNumber, int characterId, PlayerRuntimeState state,
        short originZoneId, IZoneSession zoneSession, CancellationToken cancellationToken)
    {
        var shards = await directory.GetDirectoryAsync(cancellationToken);
        foreach (var candidate in shards)
        {
            if (candidate.ShardId == options.Value.ShardId)
                continue;

            var hostedMaps = await shardMapAssignments.GetHostedMapsAsync(candidate.ShardId, cancellationToken);
            if (!hostedMaps.Contains(targetZoneNumber))
                continue;

            var targetZonePort = options.Value.ZoneBasePort + targetZoneNumber;
            if (!await reachabilityProbe.IsReachableAsync(candidate.Host, targetZonePort, cancellationToken))
            {
                logger.LogWarning(
                    "Zone-move aborted for character {CharacterId}: zone endpoint {Host}:{Port} (MapId {TargetZoneNumber}, shard {ShardId}) failed a reachability probe -- rejecting the move, character stays on {SourceMapId}",
                    characterId, candidate.Host, targetZonePort, targetZoneNumber, candidate.ShardId, originZoneId);
                zoneSession.Send(RejectedZoneMoveResponse());
                return;
            }

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
                    Port = options.Value.ZoneBasePort + targetZoneNumber
                });
                zoneSession.Send(new ReturnToHomeZoneResponse());
                return;
            }

            if (zoneSession.CurrentZone is not Zone sourceZone)
            {
                logger.LogError(
                    "Character {CharacterId}'s cross-shard handoff to zone {TargetZoneNumber} aborted: session has no current zone",
                    characterId, targetZoneNumber);
                zoneSession.Abort(DisconnectReason.Faulted);
                return;
            }

            if (zoneSession.RemoteEndPoint?.Address is not { } sourceAddress)
            {
                logger.LogWarning(
                    "Zone-move rejected for character {CharacterId}: the source address is unavailable, so its cross-shard handoff capability cannot be IP-bound",
                    characterId);
                zoneSession.Send(RejectedZoneMoveResponse());
                return;
            }

            var begin = await BeginHandoffAsync(sourceZone, characterId, targetZoneNumber, cancellationToken)
                .ConfigureAwait(false);
            if (begin.Kind != HandoffBeginOutcomeKind.Applied)
            {
                HandleUnsuccessfulHandoffBegin(zoneSession, characterId, sourceZone.MapId, targetZoneNumber,
                    begin.Kind);
                return;
            }

            var handoffCompleted = await CompleteHandoffAsync(sourceZone, zoneSession, characterId, originZoneId,
                    candidate.ShardId, targetZoneNumber, sourceAddress, begin.Snapshot!, cancellationToken)
                .ConfigureAwait(false);
            if (!handoffCompleted)
            {
                zoneSession.Send(RejectedZoneMoveResponse());
                return;
            }

            logger.LogInformation(
                "Zone {TargetZoneNumber} resolved to shard {ShardId} ({Host}:{Port}) for character {CharacterId} -- cross-shard handoff ticket minted",
                targetZoneNumber, candidate.ShardId, candidate.Host, candidate.Port, characterId);

            await LogZoneDepartureAsync(zoneSession, characterId, originZoneId, targetZoneNumber, cancellationToken);

            zoneSession.Send(new ZoneMoveResponse
            {
                Result = 0,
                Ip = candidate.Host,
                Port = options.Value.ZoneBasePort + targetZoneNumber
            });
            return;
        }

        logger.LogWarning(
            "Zone {TargetZoneNumber} is not hosted by any live shard -- refusing transfer for character {CharacterId}",
            targetZoneNumber, characterId);
        zoneSession.Send(RejectedZoneMoveResponse());
    }

    private async ValueTask<HandoffBeginOutcome> BeginHandoffAsync(Zone sourceZone, int characterId,
        short targetZoneNumber, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var snapshotSignal = new TaskCompletionSource<ZoneTransferHandoffSnapshot?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var completion =
            new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!sourceZone.Post(ZoneCommand.BeginZoneTransfer(characterId, targetZoneNumber, snapshotSignal, completion)))
        {
            logger.LogError(
                "Zone {SourceMapId} inbox full: BeginZoneTransfer for character {CharacterId} could not be queued before handoff to zone {TargetZoneNumber}",
                sourceZone.MapId, characterId, targetZoneNumber);
            return HandoffBeginOutcome.NotApplied();
        }

        ZoneCommandResult result;
        try
        {
            result = await completion.Task.WaitAsync(ZoneActorCommandCompletionTimeout, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (TimeoutException ex)
        {
            logger.LogWarning(ex,
                "Zone {SourceMapId} BeginZoneTransfer for character {CharacterId} timed out before handoff to zone {TargetZoneNumber}; reconciling actor state",
                sourceZone.MapId, characterId, targetZoneNumber);
            return await ResolveUncertainHandoffBeginAsync(sourceZone, characterId, targetZoneNumber)
                .ConfigureAwait(false);
        }

        if (result.Kind != ZoneCommandResultKind.Applied)
        {
            logger.LogWarning(
                "Zone {SourceMapId} BeginZoneTransfer for character {CharacterId} completed as {ResultKind} ({Cause}) before handoff to zone {TargetZoneNumber}",
                sourceZone.MapId, characterId, result.Kind, result.Cause, targetZoneNumber);
            return HandoffBeginOutcome.NotApplied();
        }

        try
        {
            var snapshot = await snapshotSignal.Task
                .WaitAsync(ZoneActorCommandCompletionTimeout, CancellationToken.None)
                .ConfigureAwait(false);
            if (snapshot is not null)
                return HandoffBeginOutcome.Applied(snapshot);
        }
        catch (Exception ex) when (ex is TimeoutException or InvalidOperationException)
        {
            logger.LogError(ex,
                "Zone {SourceMapId} BeginZoneTransfer for character {CharacterId} completed without a handoff snapshot",
                sourceZone.MapId, characterId);
        }

        return await ResolveUncertainHandoffBeginAsync(sourceZone, characterId, targetZoneNumber)
            .ConfigureAwait(false);
    }

    private async ValueTask<HandoffBeginOutcome> ResolveUncertainHandoffBeginAsync(Zone sourceZone, int characterId,
        short targetZoneNumber)
    {
        if (await RollbackHandoffAsync(sourceZone, characterId, null).ConfigureAwait(false))
            return HandoffBeginOutcome.NotApplied();

        logger.LogError(
            "Zone {SourceMapId} BeginZoneTransfer for character {CharacterId} toward zone {TargetZoneNumber} remains unknown after its bounded rollback observation; the session must close so source cleanup can converge through the actor",
            sourceZone.MapId, characterId, targetZoneNumber);
        return HandoffBeginOutcome.Unknown();
    }

    private void HandleUnsuccessfulHandoffBegin(IZoneSession zoneSession, int characterId, short sourceMapId,
        short targetZoneNumber, HandoffBeginOutcomeKind outcome)
    {
        if (outcome is HandoffBeginOutcomeKind.NotApplied)
        {
            zoneSession.Send(RejectedZoneMoveResponse());
            return;
        }

        logger.LogError(
            "Zone-move actor outcome is unknown for character {CharacterId} ({SourceMapId} -> {TargetZoneNumber}); aborting rather than presenting a retry response that could race an eventual actor mutation",
            characterId, sourceMapId, targetZoneNumber);
        zoneSession.Abort(DisconnectReason.ProcessingFault);
    }

    private static ZoneMoveResponse RejectedZoneMoveResponse(string ip = "", int port = 0)
    {
        return new ZoneMoveResponse { Result = 1, Ip = ip, Port = port };
    }

    private async ValueTask<bool> CompleteHandoffAsync(Zone sourceZone, IZoneSession zoneSession, int characterId,
        short sourceZoneNumber, byte targetShardId, short targetZoneNumber, IPAddress sourceAddress,
        ZoneTransferHandoffSnapshot handoff, CancellationToken cancellationToken)
    {
        var ticketMayExist = false;
        var restoreSourceDirectory = false;

        try
        {
            if (!await writeBehindFlusher.FlushCharacterNowAsync(characterId, cancellationToken, false)
                    .ConfigureAwait(false))
            {
                logger.LogWarning(
                    "Zone-move rejected for character {CharacterId}: final durable flush failed before handoff to zone {TargetZoneNumber}",
                    characterId, targetZoneNumber);
                await CompensateFailedHandoffAsync(sourceZone, zoneSession, characterId, sourceZoneNumber, handoff,
                        false, false)
                    .ConfigureAwait(false);
                return false;
            }

            ticketMayExist = true;
            var ticketCreated = await tickets.CreateAsync(zoneSession.AccountId!.Value, characterId, targetShardId,
                    options.Value.TicketTtlSeconds, zoneSession.AccountSessionToken!.Value, zoneSession.AccountGrade,
                    targetZoneNumber, sourceAddress, cancellationToken)
                .ConfigureAwait(false);

            if (!ticketCreated)
            {
                logger.LogError(
                    "Zone-move rejected for character {CharacterId}: the handoff ticket repository rejected ticket creation",
                    characterId);
                await CompensateFailedHandoffAsync(sourceZone, zoneSession, characterId, sourceZoneNumber, handoff,
                        true, false)
                    .ConfigureAwait(false);
                return false;
            }

            restoreSourceDirectory = true;
            await characterShardLocations.UpsertAsync(characterId, targetShardId, targetZoneNumber,
                    handoff.CharacterName, handoff.Tribe, cancellationToken)
                .ConfigureAwait(false);

            zoneSession.ConfirmZoneTransferHandoff();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Zone-move rejected for character {CharacterId}: handoff preparation failed for zone {TargetZoneNumber} on shard {TargetShardId}",
                characterId, targetZoneNumber, targetShardId);
            await CompensateFailedHandoffAsync(sourceZone, zoneSession, characterId, sourceZoneNumber, handoff,
                    ticketMayExist, restoreSourceDirectory)
                .ConfigureAwait(false);
            return false;
        }
    }

    private async ValueTask LogZoneDepartureAsync(IZoneSession zoneSession, int characterId, short sourceMapId,
        short targetMapId, CancellationToken cancellationToken)
    {
        try
        {
            await eventLog.LogAsync(ZoneDepartureEventCode, EventLogCategory.Session, zoneSession.AccountId,
                characterId, null, null, options.Value.ShardId, null, null, null, null, 1,
                $"SourceMapId={sourceMapId},TargetMapId={targetMapId}", cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex,
                "Failed to write game.EventLog row for zone departure (character {CharacterId}, {SourceMapId} -> {TargetMapId})",
                characterId, sourceMapId, targetMapId);
        }
    }

    private async ValueTask CompensateFailedHandoffAsync(Zone sourceZone, IZoneSession zoneSession, int characterId,
        short sourceZoneNumber, ZoneTransferHandoffSnapshot handoff, bool revokeTicket, bool restoreSourceDirectory)
    {
        if (revokeTicket)
            try
            {
                await tickets.RevokeAsync(zoneSession.AccountId!.Value, CancellationToken.None).ConfigureAwait(false);
                zoneSession.RevokeZoneTransferHandoffCommitment();
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Zone-move compensation for character {CharacterId} could not revoke the handoff ticket; keeping the source actor frozen",
                    characterId);
                zoneSession.Abort(DisconnectReason.ProcessingFault);
                return;
            }

        if (restoreSourceDirectory)
            try
            {
                await characterShardLocations.UpsertAsync(characterId, options.Value.ShardId, sourceZoneNumber,
                        handoff.CharacterName, handoff.Tribe, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Zone-move compensation for character {CharacterId} could not restore the source shard directory; keeping the source actor frozen",
                    characterId);
                zoneSession.Abort(DisconnectReason.ProcessingFault);
                return;
            }

        if (!await RollbackHandoffAsync(sourceZone, characterId, handoff.PendingRegisteredAtUtc).ConfigureAwait(false))
        {
            zoneSession.Abort(DisconnectReason.ProcessingFault);
            return;
        }

        zoneSession.ClearZoneTransferPending();
    }

    private async ValueTask<bool> RollbackHandoffAsync(Zone sourceZone, int characterId,
        DateTime? pendingRegisteredAtUtc)
    {
        var completion =
            new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = pendingRegisteredAtUtc is { } pendingRegisteredAt
            ? ZoneCommand.RollbackZoneTransfer(characterId, pendingRegisteredAt, completion)
            : ZoneCommand.ClearZoneTransferPending(characterId, completion);
        if (!sourceZone.Post(command))
        {
            logger.LogError(
                "Zone {SourceMapId} inbox full: ClearZoneTransferPending for character {CharacterId} could not be queued during handoff compensation",
                sourceZone.MapId, characterId);
            return false;
        }

        ZoneCommandResult result;
        try
        {
            result = await completion.Task.WaitAsync(ZoneActorCommandCompletionTimeout, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Zone {SourceMapId} ClearZoneTransferPending for character {CharacterId} did not complete during handoff compensation",
                sourceZone.MapId, characterId);
            return false;
        }

        if (result.Kind == ZoneCommandResultKind.Applied)
            return true;

        logger.LogError(
            "Zone {SourceMapId} ClearZoneTransferPending for character {CharacterId} completed as {ResultKind} ({Cause}) during handoff compensation",
            sourceZone.MapId, characterId, result.Kind, result.Cause);
        return false;
    }

    private enum HandoffBeginOutcomeKind : byte
    {
        Applied = 1,

        NotApplied,

        Unknown
    }

    private readonly record struct HandoffBeginOutcome(
        HandoffBeginOutcomeKind Kind,
        ZoneTransferHandoffSnapshot? Snapshot)
    {
        public static HandoffBeginOutcome Applied(ZoneTransferHandoffSnapshot snapshot)
        {
            return new HandoffBeginOutcome(HandoffBeginOutcomeKind.Applied, snapshot);
        }

        public static HandoffBeginOutcome NotApplied()
        {
            return new HandoffBeginOutcome(HandoffBeginOutcomeKind.NotApplied, null);
        }

        public static HandoffBeginOutcome Unknown()
        {
            return new HandoffBeginOutcome(HandoffBeginOutcomeKind.Unknown, null);
        }
    }
}
