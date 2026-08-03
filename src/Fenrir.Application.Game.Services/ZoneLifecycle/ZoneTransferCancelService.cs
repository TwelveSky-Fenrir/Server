using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ZoneTransferCancelService(
    ZoneRegistry zones,
    ICharacterShardLocationRepository characterShardLocations,
    IAccountSessionRepository accountSessions,
    IOptions<GameServerOptions> options,
    ILogger<ZoneTransferCancelService> logger) : IZoneTransferCancelService
{
    public async ValueTask HandleAsync(IZoneSession zoneSession, CancellationToken cancellationToken)
    {
        var characterId = zoneSession.CharacterId!.Value;

        if (zoneSession.CurrentZone is not Zone zone || !zone.TryGetPlayer(characterId, out var state) ||
            state is null || !state.IsMovingZone)
        {
            logger.LogWarning(
                "Character {CharacterId} sent ZoneTransferCancel while no zone-move was pending -- treating as a protocol violation",
                characterId);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        if (zones.TryGetPlayerInOtherZone(characterId, zone, out _, out var liveZone))
        {
            logger.LogWarning(
                "ZoneTransferCancel rejected for character {CharacterId}: already live on zone {LiveMapId} -- " +
                "the handoff already completed, refusing to resume the stale copy on zone {SourceMapId}",
                characterId, liveZone.MapId, zone.MapId);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        var accountId = zoneSession.AccountId!.Value;
        var sessionToken = zoneSession.AccountSessionToken!.Value;

        ImmutableArray<HeldAccountSessionDto> heldLeases;
        try
        {
            heldLeases = await accountSessions.RefreshAndGetHeldLeasesAsync(AccountSessionServerKind.Game,
                    options.Value.ShardId, [new AccountSessionLeaseTvp(accountId, sessionToken)], cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Failed to verify the account-session broker before honoring ZoneTransferCancel for character {CharacterId} -- disconnecting session",
                characterId);
            zoneSession.Abort(DisconnectReason.ProcessingFault);
            return;
        }

        if (heldLeases.IsEmpty)
        {
            logger.LogWarning(
                "ZoneTransferCancel rejected for character {CharacterId}: runtime.AccountSessions no longer " +
                "anchors this account to shard {ShardId} under this session's token -- a handoff completed on " +
                "another shard, refusing to resume the stale copy",
                characterId, options.Value.ShardId);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        if (!zone.Post(ZoneCommand.ClearZoneTransferPending(characterId)))
            logger.LogError(
                "Zone {MapId} inbox full: dropped ClearZoneTransferPending for character {CharacterId}",
                zone.MapId, characterId);

        zoneSession.ClearZoneTransferPending();

        try
        {
            await characterShardLocations
                .UpsertAsync(characterId, options.Value.ShardId, zone.MapId, state.Name, state.Tribe,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex,
                "Failed to refresh the shard-location directory for character {CharacterId} after cancelling its pending zone move -- disconnecting session",
                characterId);
            zoneSession.Abort(DisconnectReason.ProcessingFault);
            return;
        }

        if (!zone.Post(ZoneCommand.RefreshZoneTransferRegistrationTimestamp(characterId)))
            logger.LogError(
                "Zone {MapId} inbox full: dropped RefreshZoneTransferRegistrationTimestamp for character {CharacterId}",
                zone.MapId, characterId);

        logger.LogInformation(
            "Character {CharacterId} cancelled its pending cross-shard zone move and resumed on map {MapId}",
            characterId, zone.MapId);
    }
}
