namespace Fenrir.Application.Game.Domain.Social.Trade;

/// <summary>Soft outcomes of CZ_TRADE_ASK_SEND -- mirrors ZC_TRADE_ANSWER_RECV's pre-check codes.</summary>
public enum TradeAskOutcome
{
    Sent,
    AskerBusy,
    TargetBusy
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
    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();
    private readonly Dictionary<int, TradeSession> _sessionByCharacter = new();

    private bool IsBusy(int characterId)
    {
        return _pendingByAsker.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId) ||
               _acceptedPairs.ContainsKey(characterId) || _sessionByCharacter.ContainsKey(characterId);
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
            if (!_pendingByAsker.Remove(askerId, out targetId))
                return false;

            _pendingByTarget.Remove(targetId);
            return true;
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
}
