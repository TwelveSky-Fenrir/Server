namespace Fenrir.Application.Game.Social.Trade;

/// <summary>Soft outcomes of CZ_TRADE_ASK_SEND -- mirrors ZC_TRADE_ANSWER_RECV's pre-check codes (contracts/05_social.md: 3 soi occupé, 4 introuvable [handler-resolved], 5 cible occupée).</summary>
public enum TradeAskOutcome
{
    Sent,
    AskerBusy,
    TargetBusy
}

/// <summary>
///     Process-wide secure-trade authority (CZ_TRADE_* family, contracts/05_social.md). Same ask/cancel/
///     answer shape as <see cref="Duel.DuelRegistry" />; acceptance is SYMMETRIC (verified: "exige état 3
///     DES DEUX côtés" for CZ_TRADE_START_SEND -- both sides must have reached the accepted state, unlike
///     Mentor's asymmetric master-only start), so <see cref="TryStart" /> is callable by either accepted
///     side once both have answered.
/// </summary>
/// <remarks>
///     OPEN ISSUE, explicit and prominent (mission brief: "document exactly what's real vs out of
///     scope"): this registry and <see cref="TradeSession" /> implement the FULL negotiation lifecycle
///     (ask/cancel/answer/start/lock/end) and the atomic two-character commit
///     (<c>CharacterRepository.ExecuteTradeAsync</c>) faithfully and completely -- but the mechanism that
///     POPULATES a session's offer slots (the legacy's CZ_PROCESS_DATA_SEND tSort 218-222,
///     "Inventaire ↔ fenêtre Trade" / "Argent ↔ Trade", report 04 §1 table 2) is NOT wired into
///     <c>GenericActionHandler</c> in this pass. A trade can be negotiated, started, locked, and
///     committed end-to-end RIGHT NOW, but with whatever slots/money a caller has set directly on the
///     <see cref="TradeSession" /> -- there is no live client action that populates them yet. This is a
///     genuinely large, separately-scoped follow-up (the same container-move family ContainerMatrix's own
///     remarks already flag as "Trade/Save are NOT modeled here"), not a silent gap: the negotiation/
///     commit engine itself is real, tested code.
/// </remarks>
public sealed class TradeRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByAsker = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();
    private readonly Dictionary<int, int> _acceptedPairs = new();
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

    /// <summary>CZ_TRADE_START_SEND -- callable by either accepted side; allocates a fresh, EMPTY <see cref="TradeSession" /> for both.</summary>
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

    /// <summary>Ends (aborts OR completes) the session either participant is in -- called by CZ_TRADE_END_SEND (abort) or after a successful atomic commit (completion). Removes BOTH participants' index entries.</summary>
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
