using System.Collections.Concurrent;
using System.Collections.Immutable;
using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Domain.Simulation;
using Microsoft.Extensions.Logging;

namespace Fenrir.Application.Game.Domain.World;

public sealed partial class Zone
{
    private static readonly TimeSpan ZoneTransferStuckThreshold = TimeSpan.FromSeconds(20);

    private readonly ConcurrentDictionary<int, byte> _stuckZoneTransferResolutionsInFlight = new();

    private void SweepStuckZoneTransfers()
    {
        var now = DateTime.UtcNow;

        foreach (var (characterId, state) in _players)
        {
            if (!state.IsMovingZone || now - state.ZoneTransferRegisteredAtUtc < ZoneTransferStuckThreshold)
                continue;

            if (_stuckZoneTransferResolutionsInFlight.TryAdd(characterId, 0))
                _ = ResolveStuckZoneTransferAsync(characterId, state);
        }
    }

    private async Task ResolveStuckZoneTransferAsync(int characterId, PlayerRuntimeState state)
    {
        try
        {
            if (_zoneRegistry is not null &&
                _zoneRegistry.TryGetPlayerInOtherZone(characterId, this, out _, out var liveZone))
            {
                logger.LogWarning(
                    "Zone {MapId}: character {CharacterId} stuck mid-zone-transfer past {ThresholdSeconds}s, already " +
                    "live on zone {LiveMapId} -- evicting this stale copy instead of resuming it", MapId, characterId,
                    ZoneTransferStuckThreshold.TotalSeconds, liveZone.MapId);
                state.Session.Abort(DisconnectReason.StateViolation);
                return;
            }

            if (accountSessions is null || _sessionTickets is null || state.Session is not IZoneSession zoneSession ||
                zoneSession.AccountId is not { } accountId ||
                zoneSession.AccountSessionToken is not { } sessionToken)
            {
                logger.LogWarning(
                    "Zone {MapId}: character {CharacterId} stuck mid-zone-transfer past {ThresholdSeconds}s, but the " +
                    "broker cross-check or ticket revocation is unavailable -- leaving it stuck rather than resuming unverified; will " +
                    "retry on the next sweep", MapId, characterId, ZoneTransferStuckThreshold.TotalSeconds);
                return;
            }

            ImmutableArray<HeldAccountSessionDto> heldLeases;
            try
            {
                heldLeases = await accountSessions.RefreshAndGetHeldLeasesAsync(AccountSessionServerKind.Game,
                        options.ShardId, [new AccountSessionLeaseTvp(accountId, sessionToken)], CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Zone {MapId}: broker cross-check failed while resolving character {CharacterId}'s stuck zone " +
                    "transfer -- leaving it stuck; will retry on the next sweep", MapId, characterId);
                return;
            }

            if (!state.IsMovingZone)
                return;

            if (heldLeases.IsEmpty)
            {
                logger.LogWarning(
                    "Zone {MapId}: character {CharacterId} stuck mid-zone-transfer past {ThresholdSeconds}s, but " +
                    "runtime.AccountSessions no longer anchors this account to shard {ShardId} -- a handoff completed " +
                    "on another shard, evicting this stale copy instead of resuming it", MapId, characterId,
                    ZoneTransferStuckThreshold.TotalSeconds, options.ShardId);
                zoneSession.Abort(DisconnectReason.StateViolation);
                return;
            }

            try
            {
                await _sessionTickets.RevokeAsync(accountId, CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Zone {MapId}: failed to revoke the handoff ticket while resolving character {CharacterId}'s stuck zone transfer -- leaving it pending; will retry on the next sweep",
                    MapId, characterId);
                return;
            }

            if (!state.IsMovingZone)
                return;

            if (_zoneRegistry is not null &&
                _zoneRegistry.TryGetPlayerInOtherZone(characterId, this, out _, out var resumedZone))
            {
                logger.LogWarning(
                    "Zone {MapId}: character {CharacterId}'s transfer completed on zone {LiveMapId} while its source timeout was resolving -- evicting the source copy instead of resuming it",
                    MapId, characterId, resumedZone.MapId);
                zoneSession.Abort(DisconnectReason.StateViolation);
                return;
            }

            if (characterShardLocations is not null)
                try
                {
                    await characterShardLocations
                        .UpsertAsync(characterId, options.ShardId, MapId, state.Name, state.Tribe,
                            CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex,
                        "Zone {MapId}: failed to refresh the shard-location directory for character {CharacterId} " +
                        "while auto-resolving its stuck zone transfer; leaving it pending for the next sweep", MapId,
                        characterId);
                    return;
                }

            var clearCompletion =
                new TaskCompletionSource<ZoneCommandResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (!Post(ZoneCommand.ClearZoneTransferPending(characterId, clearCompletion)))
            {
                logger.LogError(
                    "Zone {MapId} inbox full: ClearZoneTransferPending for character {CharacterId} could not be " +
                    "queued while auto-resolving a stuck zone transfer; leaving it pending for the next sweep",
                    MapId, characterId);
                return;
            }

            ZoneCommandResult clearResult;
            try
            {
                clearResult = await clearCompletion.Task.WaitAsync(TimeSpan.FromSeconds(2), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException ex)
            {
                logger.LogWarning(ex,
                    "Zone {MapId} ClearZoneTransferPending for character {CharacterId} did not complete while " +
                    "auto-resolving a stuck zone transfer; observing its unknown eventual result", MapId,
                    characterId);
                _ = CompleteStuckZoneTransferClearAsync(characterId, zoneSession, clearCompletion.Task);
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Zone {MapId} ClearZoneTransferPending for character {CharacterId} faulted while " +
                    "auto-resolving a stuck zone transfer; leaving it pending for the next sweep", MapId,
                    characterId);
                return;
            }

            if (clearResult.Kind != ZoneCommandResultKind.Applied)
            {
                logger.LogError(
                    "Zone {MapId} ClearZoneTransferPending for character {CharacterId} completed as {ResultKind} " +
                    "({Cause}) while auto-resolving a stuck zone transfer; leaving it pending for the next sweep",
                    MapId, characterId, clearResult.Kind, clearResult.Cause);
                return;
            }

            zoneSession.ClearZoneTransferPending();

            logger.LogInformation(
                "Zone {MapId}: character {CharacterId} was stuck mid-zone-transfer past {ThresholdSeconds}s with no " +
                "handoff completed elsewhere -- auto-resumed on this zone", MapId, characterId,
                ZoneTransferStuckThreshold.TotalSeconds);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Zone {MapId}: unexpected failure auto-resolving character {CharacterId}'s stuck zone transfer",
                MapId, characterId);
        }
        finally
        {
            _stuckZoneTransferResolutionsInFlight.TryRemove(characterId, out _);
        }
    }

    private async Task CompleteStuckZoneTransferClearAsync(int characterId, IZoneSession zoneSession,
        Task<ZoneCommandResult> completion)
    {
        try
        {
            var result = await completion.ConfigureAwait(false);
            if (result.Kind != ZoneCommandResultKind.Applied)
            {
                logger.LogError(
                    "Zone {MapId} deferred ClearZoneTransferPending for character {CharacterId} completed as {ResultKind} ({Cause})",
                    MapId, characterId, result.Kind, result.Cause);
                return;
            }

            zoneSession.ClearZoneTransferPending();
            logger.LogInformation(
                "Zone {MapId}: deferred clear completed for stuck zone transfer character {CharacterId}", MapId,
                characterId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex,
                "Zone {MapId} deferred ClearZoneTransferPending faulted for character {CharacterId}", MapId,
                characterId);
        }
    }
}
