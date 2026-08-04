using Fenrir.Application.Game.Abstractions.Sessions;
using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game.ZoneLifecycle;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ZoneHandshakeService(
    ISessionTicketRepository tickets,
    IAccountSessionRepository accountSessions,
    ICharacterRepository characters,
    IBanRepository bans,
    IEventLogRepository eventLog,
    TribeQuotaRegistry tribeQuota,
    IOptions<GameServerOptions> options,
    ILogger<ZoneHandshakeService>? logger = null) : IZoneHandshakeService
{
    private const short ZoneTransferAcceptedEventCode = 3;

    public async ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId, int declaredTribe,
        IZoneSession session, CancellationToken cancellationToken)
    {
        if (!ObfuscatedUidCodec.TryDecodeAccountId(obfuscatedId, out var accountId))
        {
            logger?.LogWarning("Zone handshake rejected: malformed obfuscated id");
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);
        }

        if (accountId <= 0)
        {
            logger?.LogWarning("Zone handshake protocol violation: decoded account id {AccountId} is out of range",
                accountId);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.ProtocolViolation);
        }

        var quotaGroup = TribeQuotaGroupPolicy.ForMap(session.ListenerMapId);
        if (!TribeQuotaGate.IsDeclaredTribeInRange(quotaGroup, declaredTribe))
        {
            logger?.LogWarning(
                "Zone handshake protocol violation: declared tribe {DeclaredTribe} outside quota group {QuotaGroup}",
                declaredTribe, quotaGroup);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.ProtocolViolation);
        }

        if (session.RemoteEndPoint?.Address is not { } sourceAddress)
        {
            logger?.LogWarning("Zone handshake rejected: the source address is unavailable");
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);
        }

        var consumed = await tickets.ConsumeAsync(accountId, options.Value.ShardId, session.ListenerMapId,
            sourceAddress, cancellationToken);

        if (consumed is null)
        {
            tribeQuota.Release(session);
            logger?.LogWarning(
                "Zone handshake rejected for account {AccountId} from {SourceAddress}: session ticket absent, expired, or bound to a different source address, shard, or map",
                accountId,
                sourceAddress);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);
        }

        var character = await characters.GetForWorldEntryAsync(consumed.CharacterId, cancellationToken);
        if (character is null)
        {
            tribeQuota.Release(session);
            logger?.LogWarning(
                "Zone handshake rejected for account {AccountId}: ticket character {CharacterId} was not found",
                accountId, consumed.CharacterId);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);
        }

        if (character.AccountId != accountId)
        {
            tribeQuota.Release(session);
            logger?.LogWarning(
                "Zone handshake rejected for account {AccountId}: ticket character {CharacterId} belongs to account {CharacterAccountId}",
                accountId, consumed.CharacterId, character.AccountId);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);
        }

        bool banIsActive;
        try
        {
            banIsActive = await BanAdmissionPolicy.IsBlockedAsync(bans, accountId, consumed.CharacterId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            tribeQuota.Release(session);
            logger?.LogError(ex,
                "Zone handshake rejected for account {AccountId} character {CharacterId}: ban status could not be verified",
                accountId, consumed.CharacterId);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);
        }

        if (banIsActive)
        {
            tribeQuota.Release(session);
            logger?.LogWarning(
                "Zone handshake rejected for account {AccountId} character {CharacterId}: ban is active",
                accountId, consumed.CharacterId);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);
        }

        var canonicalTribe = (int)character.Tribe;
        if (!TribeQuotaGate.IsDeclaredTribeInRange(quotaGroup, canonicalTribe))
        {
            tribeQuota.Release(session);
            logger?.LogWarning(
                "Zone handshake protocol violation for account {AccountId}: character {CharacterId} has tribe {Tribe} " +
                "outside quota group {QuotaGroup}",
                accountId, consumed.CharacterId, canonicalTribe, quotaGroup);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.ProtocolViolation);
        }

        if (!tribeQuota.TryReserve(session, canonicalTribe, accountId, DateTimeOffset.UtcNow, quotaGroup,
                options.Value.Capacity, out var populationForCanonicalTribe))
        {
            logger?.LogWarning(
                "Zone handshake rejected for account {AccountId}: tribe {Tribe} quota full ({Population})",
                accountId, canonicalTribe, populationForCanonicalTribe);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.QuotaFull);
        }

        var transitioned = await accountSessions
            .TransitionToGameAsync(accountId, consumed.SessionToken, options.Value.ShardId, cancellationToken)
            .ConfigureAwait(false);

        if (!transitioned)
        {
            tribeQuota.Release(session);
            logger?.LogWarning(
                "Zone handshake superseded for account {AccountId} character {CharacterId}: a newer login already claimed the session",
                accountId, consumed.CharacterId);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.SessionSuperseded, accountId, consumed.CharacterId);
        }

        await eventLog.LogAsync(ZoneTransferAcceptedEventCode, EventLogCategory.Session, accountId,
            consumed.CharacterId, null, null, options.Value.ShardId, null, null, null, null, 1, null,
            cancellationToken);

        tribeQuota.Record(session, canonicalTribe, accountId, consumed.CharacterId, DateTimeOffset.UtcNow);

        return new ZoneHandshakeResult(ZoneHandshakeOutcome.Accepted, accountId, consumed.CharacterId,
            consumed.SessionToken, consumed.AccountGrade, consumed.TargetMapId);
    }
}
