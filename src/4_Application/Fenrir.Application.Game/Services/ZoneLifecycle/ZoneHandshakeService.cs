using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Application.Game;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

public sealed class ZoneHandshakeService(
    ISessionTicketRepository tickets,
    IAccountSessionRepository accountSessions,
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    TribeQuotaRegistry tribeQuota,
    IOptions<GameServerOptions> options,
    ILogger<ZoneHandshakeService>? logger = null) : IZoneHandshakeService
{
    private const short ZoneTransferAcceptedEventCode = 3;

    public async ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId, int declaredTribe,
        ZoneClientSession session, CancellationToken cancellationToken)
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

        var quotaGroup = options.Value.TribeQuotaGroup;
        if (!TribeQuotaGate.IsDeclaredTribeInRange(quotaGroup, declaredTribe))
        {
            logger?.LogWarning(
                "Zone handshake protocol violation for account {AccountId}: declared tribe {DeclaredTribe} outside quota group {QuotaGroup}",
                accountId, declaredTribe, quotaGroup);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.ProtocolViolation);
        }

        var populationForDeclaredTribe = tribeQuota.CountForTribe(declaredTribe);
        if (TribeQuotaGate.Evaluate(quotaGroup, options.Value.Capacity, populationForDeclaredTribe) ==
            TribeQuotaOutcome.QuotaFull)
        {
            logger?.LogWarning(
                "Zone handshake rejected for account {AccountId}: tribe {DeclaredTribe} quota full ({Population})",
                accountId, declaredTribe, populationForDeclaredTribe);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.QuotaFull);
        }

        var consumed = await tickets.ConsumeAsync(accountId, cancellationToken);

        if (consumed is null || consumed.ShardId != options.Value.ShardId)
        {
            logger?.LogWarning(
                "Zone handshake rejected for account {AccountId}: session ticket absent, expired, or wrong shard",
                accountId);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);
        }

        var transitioned = await accountSessions
            .TransitionToGameAsync(accountId, consumed.SessionToken, options.Value.ShardId, cancellationToken)
            .ConfigureAwait(false);

        if (!transitioned)
        {
            logger?.LogWarning(
                "Zone handshake superseded for account {AccountId} character {CharacterId}: a newer login already claimed the session",
                accountId, consumed.CharacterId);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.SessionSuperseded, accountId, consumed.CharacterId);
        }

        await eventLog.LogAsync(ZoneTransferAcceptedEventCode, EventLogCategory.Session, accountId,
            consumed.CharacterId, null, null, options.Value.ShardId, null, null, null, null, 1, null,
            cancellationToken);

        var character = await characters.GetForWorldEntryAsync(consumed.CharacterId, cancellationToken);
        var recordedTribe = (int?)character?.Tribe ?? declaredTribe;

        tribeQuota.Record(session, recordedTribe, accountId, consumed.CharacterId, DateTimeOffset.UtcNow);

        return new ZoneHandshakeResult(ZoneHandshakeOutcome.Accepted, accountId, consumed.CharacterId,
            consumed.SessionToken, consumed.AccountGrade, consumed.TargetMapId);
    }
}
