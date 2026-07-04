namespace Fenrir.Application.Game.Social.Duel;

/// <summary>Soft outcomes of CZ_DUEL_ASK_SEND -- mirrors ZC_DUEL_ANSWER_RECV's pre-check codes (contracts/05_social.md: 3 soi occupé/zone interdite, 4 introuvable [handler-resolved], 5 cible occupée).</summary>
public enum DuelAskOutcome
{
    Sent,
    ChallengerBusy, // 3 (also covers the map-124 "always refuse" gate -- see DuelRegistry's own remarks)
    TargetBusy // 5
}

/// <summary>Why an active duel ended -- <see cref="Zone.ApplyDeath" />/<c>HandleLeave</c> resolve this automatically; <c>DuelStartHandler</c>'s own explicit end path is not modeled (see <see cref="DuelRegistry" />'s class remarks: the wire protocol has no CZ_DUEL_END_SEND at all).</summary>
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
///     shape as <c>Party.PartyRegistry</c>/<c>Friends.FriendRegistry</c>; unlike those, acceptance (state
///     3) is SYMMETRIC -- both sides may send CZ_DUEL_START_SEND, whichever arrives FIRST arms the duel
///     for both (verified, contracts/05_social.md: the START handler's own guard is "exige
///     mDuelProcessState==3" on the CALLER's side only, and it immediately emits ZC_DUEL_START_RECV to
///     BOTH players -- there is no separate "the other side must also confirm" step).
/// </summary>
/// <remarks>
///     SCOPE CUT, explicit (mission brief: "document exactly what's real vs out of scope"): the wire
///     protocol has NO client-sent "give up/end duel" opcode at all (CZ 43-46 is the entire incoming
///     family) -- report 05 §0 point 5 states a duel ends via "timeout, mort, fuite" (timeout, death,
///     zone departure), all THREE of which are server-detected conditions, not client requests. This
///     pass wires DEATH (<see cref="Zone.ApplyDeath" />) and DEPARTURE (<c>Zone.HandleLeave</c>, "fuite")
///     -- both cheap, already-existing Zone hook points -- but NOT the 180 s timeout countdown
///     (ZC_DUEL_TIME_INFO ticks + auto-end), which would need a new periodic timer/hosted service this
///     pass does not add. A duel that nobody dies in or leaves therefore currently never auto-times-out
///     -- a known, documented limitation, not a silent gap.
///     <para>
///     Also NOT modeled (same "no subsystem yet" cut <c>Combat.CombatResolver</c> already documents for
///     <c>mCase</c> 1): actual duel COMBAT resolution. Two same-tribe duelists still cannot deal damage
///     to each other via the existing attack path (<c>CombatResolver.ResolveEnemyTribeAttack</c> rejects
///     same-tribe attackers outright, and <c>mCase</c> 1 is dropped unimplemented by
///     <c>Zone.ApplyCombatCommand</c>) -- this batch adds the CHALLENGE/START/END lifecycle and the
///     "potions forbidden" flag (<see cref="ActiveDuel.NoPotions" />, wired for whenever a potion-use
///     handler exists to consult it) faithfully, but does not itself unlock same-tribe PvP damage.
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

    /// <summary>CZ_DUEL_START_SEND -- callable by EITHER accepted side; the "no potions" flag is the ORIGINAL challenge's own Sort==1 choice, carried through unchanged regardless of who starts.</summary>
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
