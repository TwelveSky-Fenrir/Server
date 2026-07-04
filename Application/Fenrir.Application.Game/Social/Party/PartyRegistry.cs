namespace Fenrir.Application.Game.Social.Party;

/// <summary>Soft (non-disconnecting) outcomes of a party invite ask -- mirrors ZC_PARTY_ANSWER_RECV's pre-check codes (contracts/05_social.md CZ_PARTY_ASK_SEND).</summary>
public enum PartyInviteOutcome
{
    /// <summary>No pre-check tripped -- the ask was relayed to the target (ZC_PARTY_ASK_RECV).</summary>
    Sent,

    /// <summary>Code 3 -- the inviter itself is already mid-negotiation of another social action (this pass only models "already has a pending party ask/answer in flight").</summary>
    InviterBusy,

    /// <summary>Code 5 -- the target is busy (already mid-negotiation).</summary>
    TargetBusy,

    /// <summary>Code 6 -- the target already belongs to a party.</summary>
    TargetAlreadyPartied,

    /// <summary>
    ///     Not a soft ZC 73 code -- the legacy <c>Quit()</c>s the INVITER outright for: already partied
    ///     and not leader, a same-tribe-or-allied mismatch (alliance not modeled, see class remarks), or
    ///     a &gt;9 cumulative level gap. The caller translates this into <c>ClientSession.Abort</c>.
    /// </summary>
    InviterMustDisconnect
}

/// <summary>Outcome of accepting a pending invite (CZ_PARTY_ANSWER_SEND, Answer=0).</summary>
public enum PartyJoinOutcome
{
    /// <summary>A brand-new 2-member party was created.</summary>
    Created,

    /// <summary>Joined an existing party (a free slot was found, up to <see cref="PartyRegistry.MaxMembers" />).</summary>
    Joined,

    /// <summary>
    ///     The inviter's party was already at <see cref="PartyRegistry.MaxMembers" /> -- verified
    ///     (S04_MyWork02.cpp:1377-1385) the legacy silently drops the join with no error code at all.
    ///     Preserved verbatim, not "fixed" with an invented one.
    /// </summary>
    PartyWasFull
}

/// <summary>One party's composition -- <see cref="LeaderId" /> is always <c>Members[0]</c>. Legacy keyed parties by leader NAME (ts25center's <c>mPartyList</c>); Fenrir keys by leader CharacterId instead, a stable identity a rename/duplicate name can't violate.</summary>
public sealed class Party
{
    private readonly List<int> _members;

    internal Party(int leaderId, int firstMemberId)
    {
        _members = [leaderId, firstMemberId];
    }

    public int LeaderId => _members[0];

    public IReadOnlyList<int> Members => _members;

    internal bool TryAddMember(int characterId)
    {
        if (_members.Count >= PartyRegistry.MaxMembers)
            return false;

        _members.Add(characterId);
        return true;
    }

    /// <summary>
    ///     Removes <paramref name="characterId" /> and shifts subsequent slots down by one (verified
    ///     ts25center algorithm, S04_MyWork02.cpp:1418-1428). If the LEADER is removed this way,
    ///     <see cref="Members" />[0] silently becomes the next member -- a faithfully reproduced legacy
    ///     quirk (self-kick isn't guarded against in the source either), not a designed promotion feature.
    /// </summary>
    internal bool TryRemoveMember(int characterId)
    {
        return _members.Remove(characterId);
    }
}

/// <summary>
///     Process-wide party authority -- a party can span multiple <c>Zone</c> actors, mirroring how the
///     legacy's <c>ts25center</c> owned party state independently of any one <c>ts25zone</c> process.
///     Deliberately does NOT depend on <c>ZoneRegistry</c>/<c>Zone</c>: callers pass plain values they
///     already have, keeping this type pure and unit-testable (same posture as <c>ContainerMatrix</c>/
///     <c>CombatResolver</c>). A single lock guards every dictionary -- party actions are rare
///     (human-paced, not per-tick), so a coarse lock is fine, not a bottleneck.
/// </summary>
/// <remarks>
///     Alliance is NOT modeled (mirrors <c>Combat.CombatResolver</c>'s own documented simplification):
///     the legacy's tribe gate is <c>tribe != target.tribe &amp;&amp; tribe != ReturnAllianceTribe(target.tribe)</c>;
///     since no alliance state machine exists yet, this collapses to a plain same-tribe check, which is
///     strictly MORE restrictive than the real rule (a real alliance would let more invites through).
/// </remarks>
public sealed class PartyRegistry
{
    /// <summary><c>MAX_PARTY_AVATAR_NUM</c> (Server/Header/Protocol/DEFINE.h:610), verified.</summary>
    public const int MaxMembers = 5;

    /// <summary><c>abs((aLevel1+aLevel2) - (oLevel1+oLevel2)) &gt; 9</c> (S04_MyWork02.cpp:9608-9614), verified.</summary>
    public const int MaxLevelGap = 9;

    private readonly Lock _lock = new();

    /// <summary>Pending ask: inviter characterId -&gt; invitee characterId (legacy <c>mPartyProcessState==1</c>).</summary>
    private readonly Dictionary<int, int> _pendingByInviter = new();

    /// <summary>Pending ask, reverse index: invitee characterId -&gt; inviter characterId (legacy <c>mPartyProcessState==2</c>).</summary>
    private readonly Dictionary<int, int> _pendingByInvitee = new();

    /// <summary>Live party, keyed by leader characterId.</summary>
    private readonly Dictionary<int, Party> _partiesByLeader = new();

    /// <summary>Reverse index: any member characterId -&gt; their party's leader characterId.</summary>
    private readonly Dictionary<int, int> _leaderByMember = new();

    public bool IsInParty(int characterId)
    {
        lock (_lock)
        {
            return _leaderByMember.ContainsKey(characterId);
        }
    }

    public bool IsLeader(int characterId)
    {
        lock (_lock)
        {
            return _partiesByLeader.ContainsKey(characterId);
        }
    }

    /// <summary>Snapshot of the current member list (leader first), or empty if not partied. Safe to enumerate after the call returns -- a fresh copy, never the live list.</summary>
    public IReadOnlyList<int> GetMembers(int characterId)
    {
        lock (_lock)
        {
            if (!_leaderByMember.TryGetValue(characterId, out var leaderId) ||
                !_partiesByLeader.TryGetValue(leaderId, out var party))
                return [];

            return party.Members.ToArray();
        }
    }

    /// <summary>True while a pending ask FROM <paramref name="characterId" /> or TO <paramref name="characterId" /> exists -- the legacy's own <c>mPartyProcessState != 0</c> busy check.</summary>
    private bool IsNegotiating(int characterId)
    {
        return _pendingByInviter.ContainsKey(characterId) || _pendingByInvitee.ContainsKey(characterId);
    }

    /// <summary>
    ///     CZ_PARTY_ASK_SEND. The caller has already resolved the target by name within the inviter's
    ///     own zone (verified S04_MyWork02.cpp:9590 -- party/duel/trade/friend/mentor invites can only
    ///     ever target someone in the same zone) and gathered both sides' level/tribe/busy snapshot.
    /// </summary>
    public PartyInviteOutcome TryInvite(int inviterId, int inviterCumulativeLevel, byte inviterTribe,
        int inviteeId, int inviteeCumulativeLevel, byte inviteeTribe)
    {
        lock (_lock)
        {
            // Check order verified (S04_MyWork02.cpp:9563-9614): inviter-already-partied, then
            // target-already-partied (code 6) BEFORE the tribe/level-gap Quit()s -- a prior version checked
            // target-already-partied AFTER tribe/level, wrongly disconnecting the inviter when both were true.
            // Inviter-busy (IsNegotiating) has no direct legacy equivalent here but is kept as a self-state guard.
            if (_leaderByMember.TryGetValue(inviterId, out var inviterPartyLeader) && inviterPartyLeader != inviterId)
                return PartyInviteOutcome.InviterMustDisconnect; // already partied and not the leader

            if (IsNegotiating(inviterId))
                return PartyInviteOutcome.InviterBusy;

            if (_leaderByMember.ContainsKey(inviteeId))
                return PartyInviteOutcome.TargetAlreadyPartied;

            if (inviterTribe != inviteeTribe)
                return PartyInviteOutcome.InviterMustDisconnect;

            if (Math.Abs(inviterCumulativeLevel - inviteeCumulativeLevel) > MaxLevelGap)
                return PartyInviteOutcome.InviterMustDisconnect;

            if (IsNegotiating(inviteeId))
                return PartyInviteOutcome.TargetBusy;

            _pendingByInviter[inviterId] = inviteeId;
            _pendingByInvitee[inviteeId] = inviterId;
            return PartyInviteOutcome.Sent;
        }
    }

    /// <summary>CZ_PARTY_CANCEL_SEND -- the inviter withdraws their own still-pending ask. No-op (matching the legacy's silent <c>return</c>) if there is no such pending ask.</summary>
    public bool TryCancel(int inviterId, out int inviteeId)
    {
        lock (_lock)
        {
            if (!_pendingByInviter.Remove(inviterId, out inviteeId))
                return false;

            _pendingByInvitee.Remove(inviteeId);
            return true;
        }
    }

    /// <summary>
    ///     CZ_PARTY_ANSWER_SEND. <paramref name="accepted" />=false just clears the negotiation (matches
    ///     answer 1/2, "refuse"). <paramref name="accepted" />=true additionally performs the join (new
    ///     party if the inviter had none, else adds to the inviter's existing party) -- see
    ///     <see cref="PartyJoinOutcome" /> for the (silent, faithfully reproduced) full-party case.
    /// </summary>
    public bool TryAnswer(int inviteeId, bool accepted, out int inviterId, out PartyJoinOutcome joinOutcome)
    {
        joinOutcome = default;

        lock (_lock)
        {
            if (!_pendingByInvitee.Remove(inviteeId, out inviterId))
                return false;

            _pendingByInviter.Remove(inviterId);

            if (!accepted)
                return true;

            if (_partiesByLeader.TryGetValue(inviterId, out var existing))
            {
                if (!existing.TryAddMember(inviteeId))
                {
                    joinOutcome = PartyJoinOutcome.PartyWasFull;
                    return true; // ts25center's own silent no-op (see PartyJoinOutcome.PartyWasFull)
                }

                _leaderByMember[inviteeId] = inviterId;
                joinOutcome = PartyJoinOutcome.Joined;
                return true;
            }

            var party = new Party(inviterId, inviteeId);
            _partiesByLeader[inviterId] = party;
            _leaderByMember[inviterId] = inviterId;
            _leaderByMember[inviteeId] = inviterId;
            joinOutcome = PartyJoinOutcome.Created;
            return true;
        }
    }

    /// <summary>CZ_PARTY_LEAVE_SEND -- a non-leader member leaves voluntarily. Returns the FULL member list from BEFORE the departure (for notifying everyone who needs to know) and whether the party auto-disbanded as a result (mission brief: disband when membership drops to 1).</summary>
    public bool TryLeave(int characterId, out IReadOnlyList<int> membersBeforeLeave, out bool disbanded)
    {
        return TryRemove(characterId, characterId, requireLeader: false, out membersBeforeLeave, out disbanded);
    }

    /// <summary>CZ_PARTY_EXILE_SEND -- the leader removes <paramref name="targetId" />. Reserved to the leader (caller has already verified <paramref name="leaderId" /> IS the party leader, matching the wire contract's own "réservé au CHEF" gate); a self-targeted kick is not specially guarded (the legacy source does not either -- see <see cref="Party.TryRemoveMember" />'s own remarks).</summary>
    public bool TryKick(int leaderId, int targetId, out IReadOnlyList<int> membersBeforeKick, out bool disbanded)
    {
        return TryRemove(leaderId, targetId, requireLeader: true, out membersBeforeKick, out disbanded);
    }

    private bool TryRemove(int actingId, int targetId, bool requireLeader, out IReadOnlyList<int> membersBefore,
        out bool disbanded)
    {
        membersBefore = [];
        disbanded = false;

        lock (_lock)
        {
            if (!_leaderByMember.TryGetValue(actingId, out var leaderId) ||
                !_partiesByLeader.TryGetValue(leaderId, out var party))
                return false;

            if (requireLeader && leaderId != actingId)
                return false;

            if (!requireLeader && leaderId == actingId)
                return false; // the leader cannot LEAVE (must BREAK) -- contracts/05_social.md CZ_PARTY_LEAVE_SEND

            membersBefore = party.Members.ToArray();

            if (!party.TryRemoveMember(targetId))
                return false;

            _leaderByMember.Remove(targetId);

            if (party.Members.Count < 2)
            {
                disbanded = true;
                DisbandLocked(party);
            }

            return true;
        }
    }

    /// <summary>CZ_PARTY_BREAK_SEND -- the leader disbands the whole party unconditionally. Returns the full member list (for notifying everyone) or empty if <paramref name="leaderId" /> does not currently lead a party.</summary>
    public IReadOnlyList<int> Disband(int leaderId)
    {
        lock (_lock)
        {
            if (!_partiesByLeader.TryGetValue(leaderId, out var party))
                return [];

            var members = party.Members.ToArray();
            DisbandLocked(party);
            return members;
        }
    }

    /// <summary>Also called when a departure/kick shrinks a party below 2 members (mission brief) -- the sole remaining member is not left in a phantom 1-person "party".</summary>
    private void DisbandLocked(Party party)
    {
        foreach (var memberId in party.Members)
            _leaderByMember.Remove(memberId);

        _partiesByLeader.Remove(party.LeaderId);
    }
}
