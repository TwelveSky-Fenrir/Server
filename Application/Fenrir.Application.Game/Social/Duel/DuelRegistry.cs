namespace Fenrir.Application.Game.Social.Duel;

/// <summary>Soft outcomes of CZ_DUEL_ASK_SEND -- mirrors ZC_DUEL_ANSWER_RECV's pre-check codes.</summary>
public enum DuelAskOutcome
{
    Sent,
    ChallengerBusy, // 3 (also covers the map-124 "always refuse" gate)
    TargetBusy // 5
}

/// <summary>
///     Why an active duel ended -- resolved automatically by Zone.ApplyDeath/HandleLeave (no client end opcode
///     exists).
/// </summary>
public enum DuelEndReason
{
    Death,
    Departure
}

/// <summary>One active duel's state -- allocated at CZ_DUEL_START_SEND, freed when either side's duel ends.</summary>
public sealed record ActiveDuel(int UniqueNumber, int PlayerA, int PlayerB, bool NoPotions)
{
    public int OpponentOf(int characterId)
    {
        return characterId == PlayerA ? PlayerB : PlayerA;
    }
}

/// <summary>
///     Process-wide 1v1 duel authority. Same ask/cancel/answer shape as PartyRegistry/FriendRegistry, but
///     acceptance is symmetric -- either side's CZ_DUEL_START_SEND arms the duel for both (the legacy START
///     handler only checks state on the caller, then emits ZC_DUEL_START_RECV to both).
/// </summary>
/// <remarks>
///     The wire protocol has no client "end duel" opcode -- a duel only ends via death or departure, both
///     wired here. The 180s timeout auto-end is not implemented, so a duel nobody dies in or leaves never
///     times out. Actual duel combat is also not unlocked: same-tribe attacks are still rejected outright by
///     CombatResolver, so this wires the challenge/start/end lifecycle and the "potions forbidden" flag
///     faithfully without itself enabling same-tribe PvP damage.
/// </remarks>
public sealed class DuelRegistry
{
    /// <summary>Symmetric: both (challenger, target) and (target, challenger) entries are added on accept.</summary>
    private readonly Dictionary<int, int> _acceptedPairs = new();

    /// <summary>characterId -> the active duel it's part of (both sides point at the same ActiveDuel instance).</summary>
    private readonly Dictionary<int, ActiveDuel> _activeByCharacter = new();

    private readonly Lock _lock = new();

    /// <summary>
    ///     The original challenge's no-potions flag, keyed by challenger -- carried unchanged through to start regardless
    ///     of which side calls it.
    /// </summary>
    private readonly Dictionary<int, bool> _noPotionsByChallenger = new();

    private readonly Dictionary<int, int> _pendingByChallenger = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();

    private int _nextUniqueNumber;

    private bool IsNegotiatingOrDuelling(int characterId)
    {
        return _pendingByChallenger.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId) ||
               _acceptedPairs.ContainsKey(characterId) || _activeByCharacter.ContainsKey(characterId);
    }

    public DuelAskOutcome TryAsk(int challengerId, int targetId, bool noPotions)
    {
        lock (_lock)
        {
            if (IsNegotiatingOrDuelling(challengerId))
                return DuelAskOutcome.ChallengerBusy;
            if (IsNegotiatingOrDuelling(targetId))
                return DuelAskOutcome.TargetBusy;

            _pendingByChallenger[challengerId] = targetId;
            _pendingByTarget[targetId] = challengerId;
            _noPotionsByChallenger[challengerId] = noPotions;
            return DuelAskOutcome.Sent;
        }
    }

    public bool TryCancel(int challengerId, out int targetId)
    {
        lock (_lock)
        {
            if (!_pendingByChallenger.Remove(challengerId, out targetId))
                return false;

            _pendingByTarget.Remove(targetId);
            _noPotionsByChallenger.Remove(challengerId);
            return true;
        }
    }

    public bool TryAnswer(int targetId, bool accepted, out int challengerId)
    {
        lock (_lock)
        {
            if (!_pendingByTarget.Remove(targetId, out challengerId))
                return false;

            _pendingByChallenger.Remove(challengerId);

            if (accepted)
            {
                _acceptedPairs[challengerId] = targetId;
                _acceptedPairs[targetId] = challengerId;
            }
            else
            {
                _noPotionsByChallenger.Remove(challengerId);
            }

            return true;
        }
    }

    /// <summary>
    ///     CZ_DUEL_START_SEND -- callable by either accepted side; the original challenger's no-potions flag carries
    ///     through regardless of who starts.
    /// </summary>
    public bool TryStart(int callerId, out ActiveDuel duel)
    {
        lock (_lock)
        {
            duel = null!;

            if (!_acceptedPairs.Remove(callerId, out var opponentId))
                return false;

            _acceptedPairs.Remove(opponentId);

            var challengerId = _noPotionsByChallenger.ContainsKey(callerId) ? callerId : opponentId;
            _noPotionsByChallenger.Remove(challengerId, out var noPotions);

            duel = new ActiveDuel(++_nextUniqueNumber, callerId, opponentId, noPotions);
            _activeByCharacter[callerId] = duel;
            _activeByCharacter[opponentId] = duel;
            return true;
        }
    }

    public bool TryGetActiveDuel(int characterId, out ActiveDuel? duel)
    {
        lock (_lock)
        {
            return _activeByCharacter.TryGetValue(characterId, out duel);
        }
    }

    /// <summary>
    ///     Ends the active duel characterId is part of. Returns the opponent's characterId so the caller can notify both
    ///     sides.
    /// </summary>
    public bool TryEndActiveDuel(int characterId, out int opponentId)
    {
        lock (_lock)
        {
            opponentId = 0;

            if (!_activeByCharacter.Remove(characterId, out var duel))
                return false;

            opponentId = duel.OpponentOf(characterId);
            _activeByCharacter.Remove(opponentId);
            return true;
        }
    }
}
