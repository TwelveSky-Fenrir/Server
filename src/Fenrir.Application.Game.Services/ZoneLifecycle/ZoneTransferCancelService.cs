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

        // Broker cross-check, PART 1 (same-shard leg): the source zone's own IsMovingZone flag alone never
        // proves the handoff never completed elsewhere -- a client that keeps this OLD connection open while
        // the target zone admits it live is exactly the character-duplication exploit this guards against.
        // EnterWorldService runs the complementary check on admission (evicting a stale same-shard source
        // registration); this is the defense-in-depth backstop for any timing window that check doesn't
        // close in time, and the only signal available at all for the cross-shard leg below.
        if (zones.TryGetPlayerInOtherZone(characterId, zone, out _, out var liveZone))
        {
            logger.LogWarning(
                "ZoneTransferCancel rejected for character {CharacterId}: already live on zone {LiveMapId} -- " +
                "the handoff already completed, refusing to resume the stale copy on zone {SourceMapId}",
                characterId, liveZone.MapId, zone.MapId);
            zoneSession.Abort(DisconnectReason.StateViolation);
            return;
        }

        // Broker cross-check, PART 2 (cross-shard leg): a cross-shard handoff's target shard has no shared
        // in-process ZoneRegistry the check above can see. runtime.AccountSessions is the one piece of state
        // both shards actually share -- ZoneHandshakeService.ConsumeTicketAsync calls TransitionToGameAsync
        // on the TARGET shard the instant its handshake succeeds, which overwrites this account's single
        // AccountSessions row's ShardId to the target. If that already happened, this SOURCE shard no longer
        // holds this account's lease under this session's own token, and resuming here would create a second
        // live copy on two different shards. RefreshAndGetHeldLeasesAsync is the existing, non-destructive
        // "do I still own this lease" primitive (already used by AccountSessionLivenessHost on the Login
        // side) -- this call never mutates or consumes anything, unlike SessionTickets.ConsumeAsync.
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
