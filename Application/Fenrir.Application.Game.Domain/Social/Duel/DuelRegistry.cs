namespace Fenrir.Application.Game.Domain.Social.Duel;

/// <summary>Soft outcomes of CZ_DUEL_ASK_SEND -- mirrors ZC_DUEL_ANSWER_RECV's pre-check codes.</summary>
public enum DuelAskOutcome
{
    Sent,
    ChallengerBusy, // 3 (also covers the map-124 "always refuse" gate)
    TargetBusy, // 5

    /// <summary>
    ///     The requester's OWN <see cref="DuelRegistry" /> active-duel entry is still set -- a desynced/leaked
    ///     state, not an ordinary "busy" rejection (Server/ts25zone/S04_MyWork02.cpp:8259-8263's "already
    ///     dueling" self-check). Distinct from <see cref="ChallengerBusy" />, which covers the softer
    ///     still-negotiating (pending ask/accepted, not yet Active) case.
    /// </summary>
    ChallengerAlreadyDueling
}

/// <summary>
///     Why an active duel ended -- resolved automatically every legacy tick by
///     <see cref="Simulation.DuelMaintenanceSystem" />, never by a client-sent opcode. Numeric values mirror
///     ZC_DUEL_END_RECV's own reason byte exactly (ServerDocs/12_ts25zone/13_MyGame04_06_07_Avatar_Item_Tick.md
///     §2.5.5): 0 = time expired (draw), 1 = opponent died (this participant wins), 2 = this participant died
///     (loses), 3 = opponent could no longer be found in this zone.
/// </summary>
public enum DuelEndReason
{
    TimeExpired = 0,
    OpponentDied = 1,
    SelfDied = 2,
    OpponentNotFound = 3
}

/// <summary>One active duel's state -- allocated at CZ_DUEL_START_SEND, freed when either side's duel ends.</summary>
public sealed record ActiveDuel(int UniqueNumber, int PlayerA, int PlayerB, bool NoPotions)
{
    /// <summary>
    ///     Legacy ticks remaining before <see cref="Simulation.DuelMaintenanceSystem" />'s natural-timeout end
    ///     condition fires -- seeded at <see cref="DuelRegistry.DurationTicks" /> when the duel becomes Active
    ///     (DUEL_START_SEND, Server/ts25zone/S04_MyWork02.cpp:8400-8454's <c>mDuelProcessRemainTime = 180</c>),
    ///     decremented by however many legacy ticks elapsed since the last <see cref="Simulation.ISimulationSystem" />
    ///     pass (not always exactly 1, so a stalled host still catches up correctly). Meaningless once the
    ///     duel has ended (never re-read afterward).
    /// </summary>
    public int RemainingTicks { get; set; } = DuelRegistry.DurationTicks;

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
///     The wire protocol has no client "end duel" opcode -- a duel only ends via death, opponent departure, or
///     the 180-legacy-tick auto-timeout, all resolved once per zone tick by
///     <see cref="Simulation.DuelMaintenanceSystem" /> (Server/ts25zone/S07_MyGame04.cpp:595-682), never here
///     directly -- this class only ever holds the shared state that system reads/mutates and the registry-side
///     half of ending a duel (<see cref="TryEndActiveDuel" />). Actual duel combat is also not unlocked:
///     same-tribe attacks are still rejected outright by CombatResolver, so this wires the
///     challenge/start/end lifecycle and the "potions forbidden" flag faithfully without itself enabling
///     same-tribe PvP damage.
/// </remarks>
public sealed class DuelRegistry
{
    /// <summary>
    ///     Legacy remaining-time counter seeded onto <see cref="ActiveDuel.RemainingTicks" /> when a duel
    ///     becomes Active -- a count of legacy zone-simulation ticks (~500 ms each in production via
    ///     <see cref="Simulation.SimulationClock.LegacyTickMilliseconds" />), NOT literal seconds. The true
    ///     wall-clock duration is always this value multiplied by the zone's actual tick interval (roughly 90 s
    ///     at the shipped ~500 ms default, not 180 s) -- see <see cref="Simulation.DuelMaintenanceSystem" />'s
    ///     own remarks.
    /// </summary>
    public const int DurationTicks = 180;

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

    private bool IsNegotiating(int characterId)
    {
        return _pendingByChallenger.ContainsKey(characterId) || _pendingByTarget.ContainsKey(characterId) ||
               _acceptedPairs.ContainsKey(characterId);
    }

    private bool IsActivelyDueling(int characterId)
    {
        return _activeByCharacter.ContainsKey(characterId);
    }

    public DuelAskOutcome TryAsk(int challengerId, int targetId, bool noPotions)
    {
        lock (_lock)
        {
            // Checked first, and distinctly from the softer IsNegotiating busy-reply below: a still-set
            // active-duel entry on the REQUESTER's own side is a desynced/leaked state (Server/ts25zone/
            // S04_MyWork02.cpp:8259-8263), not an ordinary "busy" rejection -- the caller (DuelService/
            // DuelAskHandler) terminates the requester's session for this outcome instead of replying.
            if (IsActivelyDueling(challengerId))
                return DuelAskOutcome.ChallengerAlreadyDueling;
            if (IsNegotiating(challengerId))
                return DuelAskOutcome.ChallengerBusy;
            if (IsNegotiating(targetId) || IsActivelyDueling(targetId))
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
    ///     sides. The registry-side half only -- the caller (<see cref="Simulation.DuelMaintenanceSystem" />) is
    ///     responsible for the per-participant potion-restriction lift, ZC_DUEL_END_RECV send, and observer
    ///     broadcast (<c>Zone.EndActiveDuel</c>).
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

    /// <summary>
    ///     Unconditional per-character duel-state reset on every zone entry (fresh login or in-process
    ///     handoff) -- closes the narrow race where this character disconnected/transferred a tick before its
    ///     own duel state could be naturally resolved. Silent: no ZC_DUEL_END_RECV is sent and the
    ///     potion-restriction flag is deliberately left untouched here (<see cref="World.PlayerRuntimeState.CanUseConsumables" />
    ///     is shared with stun/death gating and is left to those systems' own carry-over semantics across a
    ///     zone transfer, not forced here) -- there is no meaningful "duel ended" event to report to a client
    ///     that is only now entering.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S04_MyWork02.cpp:1037-1046 (avatar zone-entry handler -- unconditional
    ///     force-reset of <c>aDuelState[]</c>/<c>mDuelProcessState</c> on every entry, independent of the
    ///     tick-based cleanup).
    ///
    ///     Pending/accepted (negotiation) state has no self-correcting per-tick mechanism of its own, so both
    ///     directions of that two-way link are torn down here (matching <see cref="TryCancel" />/
    ///     <see cref="TryAnswer" />'s own symmetric cleanup) -- this also covers the "Accepted but never
    ///     Active" edge case for whichever side re-enters first. An Active duel, by contrast, already has a
    ///     self-correcting mechanism (<see cref="Simulation.DuelMaintenanceSystem" />'s own "opponent not
    ///     found" check), so only THIS character's own key is dropped here; the opponent's own key still maps
    ///     to the same shared <see cref="ActiveDuel" /> instance and gets properly ended (with notification and
    ///     the potion-restriction lift) on the opponent's own next tick evaluation.
    ///
    ///     KNOWN TRADE-OFF: this method fires on every zone entry, including an in-process map-transfer handoff
    ///     (the same live session/socket, not a fresh connection) -- for that specific case, dropping this
    ///     character's own key here means THEY never receive their own ZC_DUEL_END_RECV (unlike their
    ///     opponent, who is still properly notified via the natural mechanism above), where relying on the
    ///     natural per-tick "opponent not found" check alone (checked against the transferring character's own
    ///     new zone) would have delivered that notification one tick later instead. Accepted deliberately: a
    ///     zone transfer already fully resets the transferring client's own local UI state, so the practical
    ///     impact of the missing packet is negligible, and the alternative (skipping the force-clear for a
    ///     same-socket handoff) would need a way to distinguish handoff from fresh-login at this call site that
    ///     does not exist today.
    /// </remarks>
    public void ForceClearOnZoneEntry(int characterId)
    {
        lock (_lock)
        {
            if (_pendingByChallenger.Remove(characterId, out var pendingTarget))
                _pendingByTarget.Remove(pendingTarget);
            if (_pendingByTarget.Remove(characterId, out var pendingChallenger))
                _pendingByChallenger.Remove(pendingChallenger);

            if (_acceptedPairs.Remove(characterId, out var acceptedPartner))
                _acceptedPairs.Remove(acceptedPartner);

            _noPotionsByChallenger.Remove(characterId);

            _activeByCharacter.Remove(characterId);
        }
    }
}
