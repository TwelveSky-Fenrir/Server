namespace Fenrir.Application.Game.Social.Duel;

/// <summary>Soft outcomes of CZ_DUEL_ASK_SEND -- mirrors ZC_DUEL_ANSWER_RECV's pre-check codes (contracts/05_social.md: 3 soi occupé/zone interdite, 4 introuvable [handler-resolved], 5 cible occupée).</summary>
public enum DuelAskOutcome
{
    Sent,
    ChallengerBusy, // 3 (also covers the map-124 "always refuse" gate -- see DuelRegistry's own remarks)
    TargetBusy // 5
}

/// <summary>Why an active duel ended -- resolved automatically by <see cref="Zone.ApplyDeath" />/<c>HandleLeave</c> (see <see cref="DuelRegistry" /> remarks: no client end opcode exists).</summary>
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
///     Process-wide 1v1 duel authority (CZ_DUEL_* family, contracts/05_social.md). Same ask/cancel/answer
///     shape as <c>Party.PartyRegistry</c>/<c>Friends.FriendRegistry</c>, but acceptance is SYMMETRIC --
///     either side's CZ_DUEL_START_SEND arms the duel for both (verified: the START handler only checks
///     "mDuelProcessState==3" on the CALLER, then emits ZC_DUEL_START_RECV to both -- no separate
///     confirm-from-both step exists).
/// </summary>
/// <remarks>
///     SCOPE CUT: the wire protocol has no client "end duel" opcode -- a duel only ends via DEATH
///     (<see cref="Zone.ApplyDeath" />) or DEPARTURE (<c>Zone.HandleLeave</c>), both wired here. The
///     180s ZC_DUEL_TIME_INFO timeout auto-end is NOT implemented (would need a new periodic
///     timer/hosted service) -- a duel nobody dies in or leaves therefore never times out.
///     <para>
///     Also NOT modeled: actual duel combat. Same-tribe attacks are still rejected outright by
///     <c>CombatResolver.ResolveEnemyTribeAttack</c>, so this batch wires the CHALLENGE/START/END
///     lifecycle and the "potions forbidden" flag (<see cref="ActiveDuel.NoPotions" />) faithfully
///     without itself unlocking same-tribe PvP damage.
///     </para>
/// </remarks>
public sealed class DuelRegistry
{
    private readonly Lock _lock = new();
    private readonly Dictionary<int, int> _pendingByChallenger = new();
    private readonly Dictionary<int, int> _pendingByTarget = new();

    /// <summary>The ORIGINAL challenge's Sort==1 flag, keyed by challenger -- carried unchanged from ask through accept to start, regardless of WHICH side ends up calling CZ_DUEL_START_SEND.</summary>
    private readonly Dictionary<int, bool> _noPotionsByChallenger = new();

    /// <summary>Symmetric: both (challenger, target) and (target, challenger) entries are added on accept, so either side's CZ_DUEL_START_SEND can consume it.</summary>
    private readonly Dictionary<int, int> _acceptedPairs = new();

    private int _nextUniqueNumber;

    /// <summary>characterId -&gt; the active duel it's part of (both sides point at the SAME <see cref="ActiveDuel" /> instance).</summary>
    private readonly Dictionary<int, ActiveDuel> _activeByCharacter = new();

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

    /// <summary>CZ_DUEL_START_SEND -- callable by either accepted side; the original challenger's no-potions flag (see <see cref="_noPotionsByChallenger" />) carries through regardless of who starts.</summary>
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

    /// <summary>Ends the active duel <paramref name="characterId" /> (if any) is part of -- called from <c>Zone.ApplyDeath</c>/<c>HandleLeave</c>. Returns the OPPONENT's characterId so the caller can notify both sides.</summary>
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
