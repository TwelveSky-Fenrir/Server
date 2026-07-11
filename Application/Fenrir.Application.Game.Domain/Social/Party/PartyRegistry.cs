namespace Fenrir.Application.Game.Domain.Social.Party;

public enum PartyInviteOutcome
{

        Sent,

        InviterBusy,

        TargetBusy,

        TargetAlreadyPartied,

        InviterMustDisconnect
}

public enum PartyJoinOutcome
{

        Created,

        Joined,

        PartyWasFull
}

public enum PartyDisconnectKind
{

        NotInParty,

        LeaderDisbanded,

        MemberLeft,

        MemberLeftAndDisbanded
}

public readonly record struct PartyDisconnectResult(
    PartyDisconnectKind Kind,
    IReadOnlyList<int> MembersBeforeLeave,
    IReadOnlyList<int> RemainingMembers)
{
    public static readonly PartyDisconnectResult NotInParty = new(PartyDisconnectKind.NotInParty, [], []);
}

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

        public bool TryRemoveMember(int characterId)
    {
        return _members.Remove(characterId);
    }
}

public sealed class PartyRegistry
{

        public const int MaxMembers = 5;

        public const int MaxLevelGap = 9;

        private readonly CrossShardNegotiationTracker _crossShard = new();

        private readonly Dictionary<int, int> _leaderByMember = new();

    private readonly Lock _lock = new();

        private readonly Dictionary<int, Party> _partiesByLeader = new();

        private readonly Dictionary<int, int> _pendingByInvitee = new();

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

        public bool IsNegotiating(int characterId)
    {
        lock (_lock)
        {
            return _pendingByInviter.ContainsKey(characterId) || _pendingByInvitee.ContainsKey(characterId) ||
                   _crossShard.IsPending(characterId);
        }
    }

        public bool TryPeekPending(int characterId, out int counterpartId, out bool isInviter)
    {
        lock (_lock)
        {
            if (_pendingByInviter.TryGetValue(characterId, out counterpartId))
            {
                isInviter = true;
                return true;
            }

            if (_pendingByInvitee.TryGetValue(characterId, out counterpartId))
            {
                isInviter = false;
                return true;
            }

            isInviter = false;
            return false;
        }
    }

        public PartyInviteOutcome TryInviteCrossShard(int inviterId, CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            if (_leaderByMember.TryGetValue(inviterId, out var inviterPartyLeader) && inviterPartyLeader != inviterId)
                return PartyInviteOutcome.InviterMustDisconnect;

            if (IsNegotiating(inviterId))
                return PartyInviteOutcome.InviterBusy;

            return _crossShard.TryRegisterOutbound(inviterId, ask)
                ? PartyInviteOutcome.Sent
                : PartyInviteOutcome.InviterBusy;
        }
    }

        public PartyInviteOutcome TryInvite(int inviterId, int inviterCumulativeLevel, byte inviterTribe,
        int inviteeId, int inviteeCumulativeLevel, byte inviteeTribe, byte? allyOfInviterTribe = null,
        bool inviteeBusyExternally = false)
    {
        lock (_lock)
        {
            if (_leaderByMember.TryGetValue(inviterId, out var inviterPartyLeader) && inviterPartyLeader != inviterId)
                return PartyInviteOutcome.InviterMustDisconnect;

            if (IsNegotiating(inviterId))
                return PartyInviteOutcome.InviterBusy;

            if (_leaderByMember.ContainsKey(inviteeId))
                return PartyInviteOutcome.TargetAlreadyPartied;

            if (inviterTribe != inviteeTribe && inviteeTribe != allyOfInviterTribe)
                return PartyInviteOutcome.InviterMustDisconnect;

            if (Math.Abs(inviterCumulativeLevel - inviteeCumulativeLevel) > MaxLevelGap)
                return PartyInviteOutcome.InviterMustDisconnect;

            if (inviteeBusyExternally || IsNegotiating(inviteeId))
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
            if (_pendingByInviter.Remove(inviterId, out inviteeId))
            {
                _pendingByInvitee.Remove(inviteeId);
                return true;
            }

            if (_crossShard.TryConsumeOutbound(inviterId, out var crossShardAsk))
            {
                inviteeId = crossShardAsk.TargetCharacterId;
                return true;
            }

            return false;
        }
    }

        public bool TryRegisterCrossShardInbound(int inviteeId, CrossShardInboundAsk ask)
    {
        lock (_lock)
        {
            if (_leaderByMember.ContainsKey(inviteeId) || IsNegotiating(inviteeId))
                return false;

            return _crossShard.TryRegisterInbound(inviteeId, ask);
        }
    }

        public bool TryConsumeCrossShardInbound(int inviteeId, out CrossShardInboundAsk ask)
    {
        lock (_lock)
        {
            return _crossShard.TryConsumeInbound(inviteeId, out ask);
        }
    }

        public bool TryConsumeCrossShardOutbound(int inviterId, out CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            return _crossShard.TryConsumeOutbound(inviterId, out ask);
        }
    }

        public PartyJoinOutcome TryCompleteCrossShardAnswer(int inviterId, int inviteeId, out IReadOnlyList<int> members)
    {
        lock (_lock)
        {
            if (_partiesByLeader.TryGetValue(inviterId, out var existing))
            {
                if (!existing.TryAddMember(inviteeId))
                {
                    members = [];
                    return PartyJoinOutcome.PartyWasFull;
                }

                _leaderByMember[inviteeId] = inviterId;
                members = existing.Members.ToArray();
                return PartyJoinOutcome.Joined;
            }

            var party = new Party(inviterId, inviteeId);
            _partiesByLeader[inviterId] = party;
            _leaderByMember[inviterId] = inviterId;
            _leaderByMember[inviteeId] = inviterId;
            members = party.Members.ToArray();
            return PartyJoinOutcome.Created;
        }
    }

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

        public bool TryLeave(int characterId, out IReadOnlyList<int> membersBeforeLeave, out bool disbanded)
    {
        return TryRemove(characterId, characterId, false, out membersBeforeLeave, out disbanded);
    }

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
                return false;

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
                        .NotInParty;

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

        private void DisbandLocked(Party party)
    {
        foreach (var memberId in party.Members)
            _leaderByMember.Remove(memberId);

        _partiesByLeader.Remove(party.LeaderId);
    }
}
