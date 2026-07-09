using Fenrir.Application.Game.Domain.Social;

namespace Fenrir.Application.Game.Domain.Social.Trade;

/// <summary>Soft outcomes of CZ_TRADE_ASK_SEND -- mirrors ZC_TRADE_ANSWER_RECV's pre-check codes.</summary>
public enum TradeAskOutcome
{
    Sent,
    AskerBusy,
    TargetBusy
}

/// <summary>
///     Which wire notification (if any) <see cref="TradeRegistry.ClearForDisconnect" />'s caller should send to
///     the partner it returns -- reuses the two notification shapes the six live opcode handlers already send,
///     so a disconnect-triggered cleanup looks identical on the wire to its voluntary counterpart.
/// </summary>
public enum TradeDisconnectNotification
{
    /// <summary>The disconnecting character had no trade-process state at all -- nothing to notify.</summary>
    None,

    /// <summary>
    ///     The disconnecting character was still negotiating (a pending ask in either direction, or an accepted
    ///     pair that never opened a trade window) -- send the partner the same empty-payload notice
    ///     <see cref="TradeRegistry.TryCancel" /> already triggers (CZ_TRADE_CANCEL_SEND's own partner notice).
    /// </summary>
    Cancel,

    /// <summary>
    ///     The disconnecting character had an open trade window -- send the partner the same "closed/aborted
    ///     without exchange" notice <see cref="TradeRegistry.TryEnd" /> already triggers (CZ_TRADE_END_SEND's own
    ///     Result=1 notice).
    /// </summary>
    End
}

/// <summary>Result of <see cref="TradeRegistry.ClearForDisconnect" />.</summary>
public readonly record struct TradeDisconnectResult(TradeDisconnectNotification Notification, int PartnerId)
{
    public static readonly TradeDisconnectResult None = new(TradeDisconnectNotification.None, 0);
}

/// <summary>
///     Process-wide secure-trade authority. Same ask/cancel/answer shape as DuelRegistry, but acceptance is
///     symmetric (CZ_TRADE_START_SEND requires "state 3" on both sides, unlike Mentor's asymmetric
///     master-only start), so <see cref="TryStart" /> is callable by either accepted side once both have
///     answered.
/// </summary>
/// <remarks>
///     The negotiation lifecycle and the atomic two-character commit are fully implemented, but the
///     mechanism that populates a session's offer slots (legacy tSort 218-222) is not wired into
///     GenericActionHandler yet -- a trade can be negotiated and committed end-to-end, but only with
///     whatever slots/money a caller sets directly on TradeSession.
/// </remarks>
public sealed class TradeRegistry
{
    private readonly Dictionary<int, int> _acceptedPairs = new();

    /// <summary>
    ///     WS1.4: this family's own cross-shard ASK-PUBLISH-ONLY half, folded into <see cref="IsBusy" /> --
    ///     see <see cref="CrossShardNegotiationTracker" />'s own remarks. Unlike Party/Friend, no
    ///     <c>ISocialCrossShardRelayHandler</c> is registered for <c>SocialCrossShardRelayKind.Trade</c> yet
    ///     (see <c>TradeInviteService.InviteAsync</c>'s own remarks for why), so only the OUTBOUND half of
    ///     this tracker is ever populated -- an inbound cross-shard trade invite can never be delivered here
    ///     today. <see cref="TryCancel" />/<see cref="ClearForDisconnect" /> still consume an outbound entry
    ///     so an asker who published a cross-shard invite that will never be answered is not stuck busy
    ///     forever.
    /// </summary>
    private readonly CrossShardNegotiationTracker _crossShard = new();

    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();
    private readonly Dictionary<int, TradeSession> _sessionByCharacter = new();

    /// <summary>
    ///     Trade-family half of the legacy <c>CheckCommunityWork</c> exclusivity check -- covers the whole
    ///     negotiation lifecycle (pending ask, accepted, and an actually-started session), matching legacy's
    ///     single nonzero-throughout trade-process-state field. Public so sibling negotiation families (e.g.
    ///     Guild ask, see <c>GuildInviteService</c>) can compose a cross-family busy check without duplicating
    ///     this registry's own state. Also includes a still-open outbound cross-shard invite.
    /// </summary>
    public bool IsBusy(int characterId)
    {
        lock (_lock)
        {
            return _pendingByAsker.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId) ||
                   _acceptedPairs.ContainsKey(characterId) || _sessionByCharacter.ContainsKey(characterId) ||
                   _crossShard.IsPending(characterId);
        }
    }

    /// <summary>
    ///     WS1.4 asker-side cross-shard invite registration (ASK-PUBLISH-ONLY -- see <see cref="_crossShard" />'s
    ///     own remarks). Same busy gate as <see cref="TryAsk" />.
    /// </summary>
    public TradeAskOutcome TryAskCrossShard(int askerId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (IsBusy(askerId))
                return TradeAskOutcome.AskerBusy;

            return _crossShard.TryRegisterOutbound(askerId, ask) ? TradeAskOutcome.Sent : TradeAskOutcome.AskerBusy;
        }
    }

    public TradeAskOutcome TryAsk(int askerId, int targetId)
    {
        lock (_lock)
        {
            if (IsBusy(askerId))
                return TradeAskOutcome.AskerBusy;
            if (IsBusy(targetId))
                return TradeAskOutcome.TargetBusy;

            _pendingByAsker[askerId] = targetId;
            _pendingByTarget[targetId] = askerId;
            return TradeAskOutcome.Sent;
        }
    }

    public bool TryCancel(int askerId, out int targetId)
    {
        lock (_lock)
        {
            if (_pendingByAsker.Remove(askerId, out targetId))
            {
                _pendingByTarget.Remove(targetId);
                return true;
            }

            if (_crossShard.TryConsumeOutbound(askerId, out var crossShardAsk))
            {
                targetId = crossShardAsk.TargetCharacterId;
                return true;
            }

            return false;
        }
    }

    public bool TryAnswer(int targetId, bool accepted, out int askerId)
    {
        lock (_lock)
        {
            if (!_pendingByTarget.Remove(targetId, out askerId))
                return false;

            _pendingByAsker.Remove(askerId);

            if (accepted)
            {
                _acceptedPairs[askerId] = targetId;
                _acceptedPairs[targetId] = askerId;
            }

            return true;
        }
    }

    /// <summary>CZ_TRADE_START_SEND -- callable by either accepted side; allocates a fresh, empty TradeSession for both.</summary>
    public bool TryStart(int callerId, out TradeSession session)
    {
        lock (_lock)
        {
            session = null!;

            if (!_acceptedPairs.Remove(callerId, out var opponentId))
                return false;

            _acceptedPairs.Remove(opponentId);

            session = new TradeSession { PlayerAId = callerId, PlayerBId = opponentId };
            _sessionByCharacter[callerId] = session;
            _sessionByCharacter[opponentId] = session;
            return true;
        }
    }

    public bool TryGetSession(int characterId, out TradeSession? session)
    {
        lock (_lock)
        {
            return _sessionByCharacter.TryGetValue(characterId, out session);
        }
    }

    /// <summary>Ends (aborts or completes) the session either participant is in. Removes both participants' index entries.</summary>
    public bool TryEnd(int characterId, out TradeSession? session)
    {
        lock (_lock)
        {
            if (!_sessionByCharacter.Remove(characterId, out session))
                return false;

            _sessionByCharacter.Remove(session.OpponentOf(characterId));
            return true;
        }
    }

    /// <summary>
    ///     CZ_TRADE_START_SEND failure-path rollback -- reverts ONLY <paramref name="callerId" />'s own
    ///     session index entry back to idle (trade-process-state 0), deliberately leaving the partner's
    ///     entry (if any) untouched even though both entries reference the same <see cref="TradeSession" />
    ///     instance committed by <see cref="TryStart" />. Used when the caller's post-commit re-validation
    ///     of either side's presence fails; the partner is left "stuck" referencing a session whose other
    ///     side has gone idle, matching legacy's asymmetric partial-effect behavior rather than a full
    ///     two-sided rollback.
    /// </summary>
    public bool TryAbortStartForCaller(int callerId)
    {
        lock (_lock)
        {
            return _sessionByCharacter.Remove(callerId);
        }
    }

    /// <summary>
    ///     Unconditional per-character trade-process-state teardown for a true disconnect (or, if ever needed
    ///     for zone-re-entry safety the way <c>DuelRegistry.ForceClearOnZoneEntry</c> is, a stale-state guard on
    ///     re-entry) -- closes the gap this registry had no counterpart to
    ///     <see cref="Duel.DuelRegistry.ForceClearOnZoneEntry" />/<c>PartyRegistry.LeaveForDisconnect</c> for: a
    ///     character who disconnects mid-negotiation (pending ask, accepted-but-not-started, or an open
    ///     <see cref="TradeSession" />) previously kept <see cref="IsBusy" /> returning true forever, since
    ///     nothing but the six live opcode handlers themselves ever removed a dictionary entry. Safe to call for
    ///     any characterId, including one with no trade-process state at all (the overwhelmingly common case, so
    ///     this stays cheap on the hot disconnect path) -- returns <see cref="TradeDisconnectResult.None" /> then.
    ///     Checks pending-as-asker, pending-as-target, accepted-pair, and open-session in that order; a
    ///     characterId can only ever be present in exactly one of those four indexes at a time (the same
    ///     invariant <see cref="IsBusy" /> already relies on), so this never needs to check more than one branch.
    /// </summary>
    /// <remarks>
    ///     Fenrir-only robustness fix, not a specific legacy line-for-line reproduction -- no
    ///     <c>Server/path:line</c> citation was supplied or independently located in this task for Trade's own
    ///     disconnect-cleanup analogue of the legacy Exit handler (contrast
    ///     <c>PartyRegistry.LeaveForDisconnect</c>'s own remarks, which cites
    ///     <c>Server/ts25zone/S03_MyUser.cpp:338-411</c> for party specifically). Flagged for a
    ///     <c>cpp-ts25-explorer</c> re-check if the exact legacy Exit-handler behavior for
    ///     <c>mTradeProcessState</c> on disconnect becomes relevant; this method exists to close a
    ///     stuck-forever-busy availability bug regardless of the precise legacy notification semantics on that
    ///     path.
    /// </remarks>
    public TradeDisconnectResult ClearForDisconnect(int characterId)
    {
        lock (_lock)
        {
            if (_pendingByAsker.Remove(characterId, out var target))
            {
                _pendingByTarget.Remove(target);
                return new TradeDisconnectResult(TradeDisconnectNotification.Cancel, target);
            }

            if (_pendingByTarget.Remove(characterId, out var asker))
            {
                _pendingByAsker.Remove(asker);
                return new TradeDisconnectResult(TradeDisconnectNotification.Cancel, asker);
            }

            if (_acceptedPairs.Remove(characterId, out var acceptedPartner))
            {
                _acceptedPairs.Remove(acceptedPartner);
                return new TradeDisconnectResult(TradeDisconnectNotification.Cancel, acceptedPartner);
            }

            if (_sessionByCharacter.Remove(characterId, out var session))
            {
                var opponentId = session.OpponentOf(characterId);
                _sessionByCharacter.Remove(opponentId);
                return new TradeDisconnectResult(TradeDisconnectNotification.End, opponentId);
            }

            // WS1.4: a disconnecting character's own outbound cross-shard invite (this registry only ever
            // holds the OUTBOUND half, see _crossShard's own remarks) is torn down here too, for the same
            // stuck-forever-busy reason every branch above already exists for -- no target-side handler
            // exists yet to ever deliver the Answer that would normally consume this entry. No partner to
            // notify (the partner is remote, and there is no Cancel message shape in this relay -- see
            // ISocialCrossShardRelayHandler.HandleAnswerAsync's own remarks on tolerating exactly this), so
            // this is a pure cleanup with no distinct TradeDisconnectResult shape of its own.
            _crossShard.TryConsumeOutbound(characterId, out _);
            return TradeDisconnectResult.None;
        }
    }
}
