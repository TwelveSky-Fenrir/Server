namespace Fenrir.Application.Game.Domain.Social.Party;

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

    /// <summary>
    ///     The inviter's party was already full -- the legacy silently drops the join with no error code at all,
    ///     preserved verbatim.
    /// </summary>
    PartyWasFull
}

/// <summary>Discriminator for how <see cref="PartyRegistry.LeaveForDisconnect" /> resolved.</summary>
public enum PartyDisconnectKind
{
    /// <summary>The disconnecting character had no live party at all -- nothing to do.</summary>
    NotInParty,

    /// <summary>
    ///     The disconnecting character was the party's leader -- the whole party was broken, same effect as
    ///     <see cref="PartyRegistry.Disband" />.
    /// </summary>
    LeaderDisbanded,

    /// <summary>The disconnecting character was an ordinary member -- only they were removed; the party survives.</summary>
    MemberLeft,

    /// <summary>
    ///     The disconnecting character was an ordinary member whose departure dropped the party below 2 --
    ///     auto-disbanded, same "no lone-leader phantom party" rule <see cref="PartyRegistry.TryLeave" /> already
    ///     applies to a voluntary leave.
    /// </summary>
    MemberLeftAndDisbanded
}

/// <summary>Result of <see cref="PartyRegistry.LeaveForDisconnect" />.</summary>
public readonly record struct PartyDisconnectResult(
    PartyDisconnectKind Kind,
    IReadOnlyList<int> MembersBeforeLeave,
    IReadOnlyList<int> RemainingMembers)
{
    public static readonly PartyDisconnectResult NotInParty = new(PartyDisconnectKind.NotInParty, [], []);
}

/// <summary>
///     One party's composition -- LeaderId is always Members[0]. Legacy keyed parties by leader name;
///     Fenrir keys by leader CharacterId instead, a stable identity a rename/duplicate name can't violate.
/// </summary>
public sealed class Party
{
    private readonly List<int> _members;

    public Party(int leaderId, int firstMemberId)
    {
        _members = [leaderId, firstMemberId];
    }

    public int LeaderId => _members[0];

    public IReadOnlyList<int> Members => _members;

    public bool TryAddMember(int characterId)
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
    public bool TryRemoveMember(int characterId)
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
///     <para>
///         CROSS-SHARD SCOPE (Fenrir architecture note, not a legacy citation -- flagged by the
///         center-world-authority "process-wide-singleton-not-cross-shard-synced" review, 2026-07): "process-wide"
///         above means one instance per running <c>GameServer</c> PROCESS (<c>AddSingleton</c>), not one per
///         cluster. The legacy's single <c>ts25center</c> hub relayed every party mutation to every connected
///         zone process (<c>ZONE_BROADCAST_FOR_RELAY_SEND</c>, <c>Server/ts25center/S04_MyWork02.cpp:1317-1605</c>),
///         so a party split across two zone processes was always visible identically from either side. Fenrir's
///         map-based sharding means two party members can be handled by two different running processes with no
///         equivalent relay: an invite/answer/leave/kick/disband mutates only the registry instance in the
///         process that received the packet, and nothing propagates that mutation to any other process's copy --
///         no error, no signal to the caller that a fuller view might exist elsewhere. The only place this gap is
///         currently compensated is party-NAME resolution (<see cref="PartyIdentityResolver" />'s own documented
///         "leader on a different zone shard" fallback, reporting the requester's own name instead of crashing or
///         silently corrupting state); full membership/negotiation state has no such fallback and remains an
///         open, unaddressed cross-shard gap, not a verified-safe design.
///     </para>
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

    /// <summary>
    ///     Party-family half of the legacy <c>CheckCommunityWork</c> exclusivity check -- pending negotiation
    ///     only, deliberately NOT the same as <see cref="IsInParty" /> (already belonging to a party is not one
    ///     of the seven exclusivity flags). Public so sibling negotiation families (e.g. Guild ask, see
    ///     <c>GuildInviteService</c>) can compose a cross-family busy check without duplicating this registry's
    ///     own state.
    /// </summary>
    public bool IsNegotiating(int characterId)
    {
        lock (_lock)
        {
            return _pendingByInviter.ContainsKey(characterId) || _pendingByInvitee.ContainsKey(characterId);
        }
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

    /// <summary>
    ///     accepted=true additionally performs the join -- new party if the inviter had none, else adds to the inviter's
    ///     existing party.
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

    /// <summary>
    ///     CZ_PARTY_LEAVE_SEND -- a non-leader member leaves voluntarily. Returns the member list from before the
    ///     departure.
    /// </summary>
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

    /// <summary>
    ///     Disconnect/logout cleanup step (behavior contract "session-state-machine", the ready and
    ///     non-mid-transfer disconnect branch): "any active party is broken ... distinguishing member left from
    ///     leader left". Unlike the explicit CZ_PARTY_LEAVE_SEND/CZ_PARTY_BREAK_SEND opcodes, the caller here
    ///     does not already know whether <paramref name="characterId" /> is the leader or an ordinary member, so
    ///     this single entry point dispatches to the same effect either <see cref="Disband" /> (leader) or
    ///     <see cref="TryLeave" /> (member, including its own below-2-members auto-disband rule) would have had,
    ///     and reports which one happened so the caller can send the matching wire notification. A no-op
    ///     (<see cref="PartyDisconnectResult.NotInParty" />) if the character has no live party at all.
    /// </summary>
    /// <remarks>
    ///     Réf. C++ : Server/ts25zone/S03_MyUser.cpp:338-411,404 (Exit -- party break + relay, distinguishing
    ///     member-left/leader-left by comparing the avatar's own name against the party name). Fenrir keys
    ///     parties by leader CharacterId rather than a per-avatar party-name field (see this class's own
    ///     remarks), so the distinction is made structurally here instead (was <paramref name="characterId" />
    ///     the party's LeaderId) -- the same substitution every other party-changed notification in this
    ///     codebase already makes.
    /// </remarks>
    public PartyDisconnectResult LeaveForDisconnect(int characterId)
    {
        lock (_lock)
        {
            if (!_leaderByMember.TryGetValue(characterId, out var leaderId) ||
                !_partiesByLeader.TryGetValue(leaderId, out var party))
                return PartyDisconnectResult.NotInParty;

            if (leaderId == characterId)
            {
                var members = party.Members.ToArray();
                DisbandLocked(party);
                return new PartyDisconnectResult(PartyDisconnectKind.LeaderDisbanded, members, []);
            }

            var membersBeforeLeave = party.Members.ToArray();

            if (!party.TryRemoveMember(characterId))
                return
                    PartyDisconnectResult
                        .NotInParty; // unreachable given the _leaderByMember check above; defensive only

            _leaderByMember.Remove(characterId);

            if (party.Members.Count < 2)
            {
                DisbandLocked(party);
                return new PartyDisconnectResult(PartyDisconnectKind.MemberLeftAndDisbanded, membersBeforeLeave, []);
            }

            return new PartyDisconnectResult(PartyDisconnectKind.MemberLeft, membersBeforeLeave,
                party.Members.ToArray());
        }
    }

    /// <summary>
    ///     Also called when a departure/kick shrinks a party below 2 members, so nobody is left in a phantom 1-person
    ///     "party".
    /// </summary>
    private void DisbandLocked(Party party)
    {
        foreach (var memberId in party.Members)
            _leaderByMember.Remove(memberId);

        _partiesByLeader.Remove(party.LeaderId);
    }
}
