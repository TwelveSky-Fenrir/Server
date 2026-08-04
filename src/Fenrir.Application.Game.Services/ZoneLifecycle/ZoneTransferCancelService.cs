using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.Simulation;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ZoneTransferCancelService(
    ZoneRegistry zones,
    ICharacterShardLocationRepository characterShardLocations,
    IAccountSessionRepository accountSessions,
    ISessionTicketRepository tickets,
    IOptions<GameServerOptions> options,
    ILogger<ZoneTransferCancelService> logger) : IZoneTransferCancelService
{
    private static readonly TimeSpan ZoneActorCommandCompletionTimeout = TimeSpan.FromSeconds(2);

    public async ValueTask HandleAsync(IZoneSession zoneSession, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var characterId = zoneSession.CharacterId!.Value;
        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null || !state.IsMovingZone || state.Session.SessionId != zoneSession.SessionId)
        {
            logger.LogWarning(
                "Character {CharacterId} sent ZoneTransferCancel while its exact source session had no pending zone move -- treating as a protocol violation",
                characterId);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        if (zones.TryGetPlayerInOtherZone(characterId, zone, out _, out var liveZone))
        {
            logger.LogWarning(
                "ZoneTransferCancel rejected for character {CharacterId}: already live on zone {LiveMapId}; refusing to resume the stale source copy on zone {SourceMapId}",
                characterId, liveZone.MapId, zone.MapId);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        var accountId = zoneSession.AccountId!.Value;
        var sessionToken = zoneSession.AccountSessionToken!.Value;
        var pendingRegisteredAtUtc = state.ZoneTransferRegisteredAtUtc;
        var characterName = state.Name;
        var tribe = state.Tribe;

        if (!await IsSourceLeaseHeldAsync(accountId, sessionToken).ConfigureAwait(false))
        {
            logger.LogWarning(
                "ZoneTransferCancel rejected for character {CharacterId}: runtime.AccountSessions no longer anchors this token to source shard {ShardId}",
                characterId, options.Value.ShardId);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        try
        {
            await tickets.RevokeAsync(accountId, CancellationToken.None).ConfigureAwait(false);
            zoneSession.RevokeZoneTransferHandoffCommitment();
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "ZoneTransferCancel could not revoke the handoff ticket for character {CharacterId}; actor outcome remains transfer-pending",
                characterId);
            zoneSession.Abort(DisconnectReason.ProcessingFault);
            return;
        }

        if (!await IsSourceLeaseHeldAsync(accountId, sessionToken).ConfigureAwait(false))
        {
            logger.LogWarning(
                "ZoneTransferCancel rejected for character {CharacterId}: the destination consumed the handoff before revocation completed",
                characterId);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        if (zones.TryGetPlayerInOtherZone(characterId, zone, out _, out liveZone))
        {
            logger.LogWarning(
                "ZoneTransferCancel rejected for character {CharacterId}: a destination player is already live on zone {LiveMapId}",
                characterId, liveZone.MapId);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        try
        {
            await characterShardLocations.UpsertAsync(characterId, options.Value.ShardId, zone.MapId, characterName,
                    tribe, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "ZoneTransferCancel could not restore the source shard directory for character {CharacterId}",
                characterId);
            zoneSession.Abort(DisconnectReason.ProcessingFault);
            return;
        }

        var completion =
            new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!zone.Post(ZoneCommand.RollbackZoneTransfer(characterId, pendingRegisteredAtUtc, completion)))
        {
            logger.LogError(
                "Zone {MapId} core inbox backpressured ZoneTransferCancel rollback for character {CharacterId}; closing so disconnect cleanup can converge",
                zone.MapId, characterId);
            zoneSession.Abort(DisconnectReason.ProcessingFault);
            return;
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
                "ZoneTransferCancel rollback for character {CharacterId} timed out after actor admission; closing for ordered actor cleanup",
                characterId);
            zoneSession.Abort(DisconnectReason.ProcessingFault);
            return;
        }

        if (result.Kind != ZoneCommandResultKind.Applied)
        {
            logger.LogWarning(
                "ZoneTransferCancel rollback for character {CharacterId} completed as {ResultKind} ({Cause}); closing for ordered actor cleanup",
                characterId, result.Kind, result.Cause);
            zoneSession.Abort(DisconnectReason.ProcessingFault);
            return;
        }

        if (!await IsSourceLeaseHeldAsync(accountId, sessionToken).ConfigureAwait(false) ||
            zones.TryGetPlayerInOtherZone(characterId, zone, out _, out liveZone))
        {
            logger.LogWarning(
                "ZoneTransferCancel rollback for character {CharacterId} raced destination admission (live map {LiveMapId}); closing the source copy",
                characterId, liveZone?.MapId);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        zoneSession.ClearZoneTransferPending();
        logger.LogInformation(
            "Character {CharacterId} cancelled its pending zone move and resumed on source map {MapId}", characterId,
            zone.MapId);
    }

    private async ValueTask<bool> IsSourceLeaseHeldAsync(int accountId, Guid sessionToken)
    {
        try
        {
            var heldLeases = await accountSessions.RefreshAndGetHeldLeasesAsync(AccountSessionServerKind.Game,
                    options.Value.ShardId, [new AccountSessionLeaseTvp(accountId, sessionToken)],
                    CancellationToken.None)
                .ConfigureAwait(false);
            return !heldLeases.IsEmpty;
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Failed to verify runtime.AccountSessions while reconciling ZoneTransferCancel for account {AccountId}",
                accountId);
            return false;
        }
    }
}
