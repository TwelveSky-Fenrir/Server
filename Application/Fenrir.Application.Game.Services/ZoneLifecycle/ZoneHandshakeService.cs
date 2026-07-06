using Fenrir.Application.Game.Abstractions.ZoneLifecycle;
using Fenrir.Application.Game.Domain;
using Fenrir.Application.Game.Domain.World.ZoneWar;
using Fenrir.Network.Dispatch.Sessions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Fenrir.Application.Game.Services.ZoneLifecycle;

/// <remarks>
///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:572-744 (full handler body: quota gate, upstream call, side
///     effects on success) ; Server/Header/function.h:38-44 (valid-index-range precondition on the parsed
///     account/session index) ; see <see cref="TribeQuotaGate" />/<see cref="TribeQuotaRegistry" /> for the
///     quota-gate citations proper.
/// </remarks>
public sealed class ZoneHandshakeService(
    ISessionTicketRepository tickets,
    IAccountSessionRepository accountSessions,
    ICharacterRepository characters,
    IEventLogRepository eventLog,
    TribeQuotaRegistry tribeQuota,
    IOptions<GameServerOptions> options,
    ILogger<ZoneHandshakeService>? logger = null) : IZoneHandshakeService
{
    /// <summary>
    ///     game.EventLog.EventCode for a successfully-consumed zone-transfer ticket (Category=Session) -- see
    ///     Fenrir.Application.Login.Services.Login.LoginService's LoginSucceededEventCode remarks for the full
    ///     four-code cross-reference within this category. Only the Accepted outcome is logged here: Rejected
    ///     (malformed/absent/expired/wrong-shard ticket) and SessionSuperseded (a newer login already claimed
    ///     the account) are the cross-process duplicate-login kick/refusal infrastructure's own concern, not
    ///     duplicated here.
    /// </summary>
    private const short ZoneTransferAcceptedEventCode = 3;

    public async ValueTask<ZoneHandshakeResult> ConsumeTicketAsync(string obfuscatedId, int declaredTribe,
        ZoneClientSession session, CancellationToken cancellationToken)
    {
        if (!ObfuscatedUidCodec.TryDecodeAccountId(obfuscatedId, out var accountId))
        {
            logger?.LogWarning("Zone handshake rejected: malformed obfuscated id");
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);
        }

        // function.h:38-44's valid-index-range precondition -- distinct from the malformed-string case above:
        // a well-formed "MG"+digits identifier that parses to an impossible account/session slot is a protocol
        // violation (silent drop), not a generic rejection response. Only a lower bound is enforced here: the
        // translated contract names the precondition but not a concrete upper bound, so none is fabricated --
        // see this class's own remarks for the citation.
        if (accountId <= 0)
        {
            logger?.LogWarning("Zone handshake protocol violation: decoded account id {AccountId} is out of range",
                accountId);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.ProtocolViolation);
        }

        // Repeating this request on an already-registered connection is a protocol violation too, but it never
        // reaches here: ZoneHandshakeRequest.AllowedStates is Connected-only, so SessionStateGate already drops
        // any replay before dispatch (see ZoneHandshakeHandler's own remarks).
        var quotaGroup = options.Value.TribeQuotaGroup;
        if (!TribeQuotaGate.IsDeclaredTribeInRange(quotaGroup, declaredTribe))
        {
            logger?.LogWarning(
                "Zone handshake protocol violation for account {AccountId}: declared tribe {DeclaredTribe} outside quota group {QuotaGroup}",
                accountId, declaredTribe, quotaGroup);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.ProtocolViolation);
        }

        // No independently maintained counter -- recomputed by scanning every currently temp-registered
        // connection's recorded (always upstream-resolved, never declared) tribe at the moment of this attempt.
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

        // Refuse absent/expired/wrong-shard tickets identically -- the closest structural analog to legacy's
        // RegisterUserForZone_00 failure, so ZoneHandshakeHandler now answers this with the same silent-drop
        // posture legacy uses there (see ZoneHandshakeOutcome.Rejected's own remarks), not QuotaFull's explicit
        // Result=1.
        if (consumed is null || consumed.ShardId != options.Value.ShardId)
        {
            logger?.LogWarning(
                "Zone handshake rejected for account {AccountId}: session ticket absent, expired, or wrong shard",
                accountId);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.Rejected);
        }

        // Cross-process duplicate-login authority: proves this world-entry claim is for the same login epoch that
        // minted the ticket, not a hijack of a newer login (runtime.AccountSessions moved on since this ticket was
        // issued -- e.g. the account logged in again elsewhere before this ticket got consumed).
        var transitioned = await accountSessions
            .TransitionToGameAsync(accountId, consumed.SessionToken, options.Value.ShardId, cancellationToken)
            .ConfigureAwait(false);

        if (!transitioned)
        {
            logger?.LogWarning(
                "Zone handshake superseded for account {AccountId} character {CharacterId}: a newer login already claimed the session",
                accountId, consumed.CharacterId);
            return new ZoneHandshakeResult(ZoneHandshakeOutcome.SessionSuperseded);
        }

        // Session-lifecycle audit row: this is the "zone-transfer" leg (see ZoneTransferAcceptedEventCode's
        // remarks for the other three legs).
        await eventLog.LogAsync(ZoneTransferAcceptedEventCode, EventLogCategory.Session, accountId,
            consumed.CharacterId, null, null, options.Value.ShardId, null, null, null, null, 1, null,
            cancellationToken);

        // GAP: the tribe recorded for population-counting purposes must be the character's own persisted tribe,
        // never the declared tribe just used for the threshold check above (the two can legitimately disagree
        // -- see TribeQuotaRegistry's own remarks). Fenrir's session ticket (ADR-0005) does not itself carry
        // the character's tribe, so it is resolved here via the same usp_Character_GetForWorldEntry read
        // EnterWorldService performs again moments later at avatar-selection time -- an accepted, once-per-
        // session extra round trip (same posture as EnterWorldService's own cross-shard-directory upsert), not
        // a new stored procedure. Embedding Tribe directly in the ticket (mirroring how AccountGrade was added
        // to it) would remove this redundant read, but that is a schema change for fenrir-database-engineer,
        // not done here. A null character here is already anomalous (the ticket names a character that should
        // exist) -- fall back to the declared tribe defensively rather than fail an otherwise-successful handshake.
        var character = await characters.GetForWorldEntryAsync(consumed.CharacterId, cancellationToken);
        var recordedTribe = (int?)character?.Tribe ?? declaredTribe;

        tribeQuota.Record(session, recordedTribe, accountId, consumed.CharacterId, DateTimeOffset.UtcNow);

        return new ZoneHandshakeResult(ZoneHandshakeOutcome.Accepted, accountId, consumed.CharacterId,
            consumed.SessionToken, consumed.AccountGrade);
    }
}
