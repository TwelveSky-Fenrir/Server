namespace Fenrir.Application.Game.Social.Party;

/// <summary>Soft (non-disconnecting) outcomes of a party invite ask -- mirrors ZC_PARTY_ANSWER_RECV's pre-check codes.</summary>
public enum PartyInviteOutcome
{
    /// <summary>No pre-check tripped -- the ask was relayed to the target (ZC_PARTY_ASK_RECV).</summary>
    Sent,

    /// <summary>Code 3 -- the inviter is already mid-negotiation of another social action.</summary>
    InviterBusy,

    /// <summary>Code 5 -- the target is busy (already mid-negotiation).</summary>
    TargetBusy,

    /// <summary>Code 6 -- the target already belongs to a party.</summary>
    TargetAlreadyPartied,

    /// <summary>
    ///     Not a soft ZC 73 code -- the legacy Quit()s the inviter outright for: already partied and not
    ///     leader, a same-tribe-or-allied mismatch (alliance not modeled), or a &gt;9 cumulative level gap.
    ///     The caller translates this into ClientSession.Abort.
    /// </summary>
    InviterMustDisconnect
}

/// <summary>Outcome of accepting a pending invite (CZ_PARTY_ANSWER_SEND, Answer=0).</summary>
public enum PartyJoinOutcome
{
    /// <summary>A brand-new 2-member party was created.</summary>
    Created,

    /// <summary>Joined an existing party.</summary>
    Joined,

    /// <summary>The inviter's party was already full -- the legacy silently drops the join with no error code at all, preserved verbatim.</summary>
    PartyWasFull
}

/// <summary>
///     One party's composition -- LeaderId is always Members[0]. Legacy keyed parties by leader name;
///     Fenrir keys by leader CharacterId instead, a stable identity a rename/duplicate name can't violate.
/// </summary>
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
    ///     Removes and shifts subsequent slots down by one. If the leader is removed this way, Members[0]
    ///     silently becomes the next member -- a faithfully reproduced legacy quirk, not a designed
    ///     promotion feature.
    /// </summary>
    internal bool TryRemoveMember(int characterId)
    {
        return _members.Remove(characterId);
    }
}

/// <summary>
///     Process-wide party authority -- a party can span multiple Zone actors, mirroring how the legacy's
///     ts25center owned party state independently of any one ts25zone process. A single lock guards every
///     dictionary -- party actions are human-paced, not per-tick, so a coarse lock is fine.
/// </summary>
/// <remarks>
///     Alliance is not modeled: the legacy tribe gate also checks ReturnAllianceTribe, but with no alliance
///     state machine this collapses to a plain same-tribe check, strictly more restrictive than the real rule.
/// </remarks>
public sealed class PartyRegistry
{
    /// <summary>MAX_PARTY_AVATAR_NUM (DEFINE.h:610).</summary>
    public const int MaxMembers = 5;

    /// <summary>abs((aLevel1+aLevel2) - (oLevel1+oLevel2)) &gt; 9 (S04_MyWork02.cpp:9608-9614).</summary>
    public const int MaxLevelGap = 9;

    /// <summary>Reverse index: any member characterId -> their party's leader characterId.</summary>
    private readonly Dictionary<int, int> _leaderByMember = new();

    private readonly Lock _lock = new();

    /// <summary>Live party, keyed by leader characterId.</summary>
    private readonly Dictionary<int, Party> _partiesByLeader = new();

    /// <summary>Pending ask, reverse index: invitee characterId -> inviter characterId.</summary>
    private readonly Dictionary<int, int> _pendingByInvitee = new();

    /// <summary>Pending ask: inviter characterId -> invitee characterId.</summary>
    private readonly Dictionary<int, int> _pendingByInviter = new();

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

    /// <summary>Snapshot of the current member list (leader first), or empty if not partied.</summary>
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

    private bool IsNegotiating(int characterId)
    {
        return _pendingByInviter.ContainsKey(characterId) || _pendingByInvitee.ContainsKey(characterId);
    }

    /// <summary>
    ///     CZ_PARTY_ASK_SEND. The caller has already resolved the target by name within the inviter's own
    ///     zone and gathered both sides' level/tribe/busy snapshot.
    /// </summary>
    public PartyInviteOutcome TryInvite(int inviterId, int inviterCumulativeLevel, byte inviterTribe,
        int inviteeId, int inviteeCumulativeLevel, byte inviteeTribe)
    {
        lock (_lock)
        {
            // Check order verified: inviter-already-partied, then target-already-partied (code 6) BEFORE the
            // tribe/level-gap Quit()s -- checking them after would wrongly disconnect the inviter when both are true.
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

    /// <summary>accepted=true additionally performs the join -- new party if the inviter had none, else adds to the inviter's existing party.</summary>
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
                    return true;
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

    /// <summary>CZ_PARTY_LEAVE_SEND -- a non-leader member leaves voluntarily. Returns the member list from before the departure.</summary>
    public bool TryLeave(int characterId, out IReadOnlyList<int> membersBeforeLeave, out bool disbanded)
    {
        return TryRemove(characterId, characterId, false, out membersBeforeLeave, out disbanded);
    }

    /// <summary>CZ_PARTY_EXILE_SEND -- reserved to the leader (caller has already verified leaderId IS the leader).</summary>
    public bool TryKick(int leaderId, int targetId, out IReadOnlyList<int> membersBeforeKick, out bool disbanded)
    {
        return TryRemove(leaderId, targetId, true, out membersBeforeKick, out disbanded);
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
                return false; // the leader cannot LEAVE (must BREAK)

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

    /// <summary>CZ_PARTY_BREAK_SEND -- the leader disbands the whole party unconditionally.</summary>
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

    /// <summary>Also called when a departure/kick shrinks a party below 2 members, so nobody is left in a phantom 1-person "party".</summary>
    private void DisbandLocked(Party party)
    {
        foreach (var memberId in party.Members)
            _leaderByMember.Remove(memberId);

        _partiesByLeader.Remove(party.LeaderId);
    }
}
