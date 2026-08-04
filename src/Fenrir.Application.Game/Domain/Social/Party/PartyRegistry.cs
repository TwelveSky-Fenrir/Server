using System.Diagnostics.CodeAnalysis;

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

public readonly record struct PartyMember(int CharacterId, string Name);

public sealed class Party
{
    private readonly List<PartyMember> _members;

    public Party(PartyMember leader, PartyMember firstMember)
    {
        LeaderId = leader.CharacterId;
        _members = [leader, firstMember];
    }

    public Party(int leaderId, List<PartyMember> members)
    {
        LeaderId = leaderId;
        _members = members;
    }

    public int LeaderId { get; }

    public IReadOnlyList<PartyMember> Members => _members;

    public bool TryAddMember(PartyMember member)
    {
        var existing = IndexOf(member.CharacterId);
        if (existing >= 0)
        {
            if (member.Name.Length > 0)
                _members[existing] = member;
            return true;
        }

        if (_members.Count >= PartyRegistry.MaxMembers)
            return false;

        _members.Add(member);
        return true;
    }

    public bool TryRemoveMember(int characterId)
    {
        if (characterId == LeaderId)
            return false;

        var index = IndexOf(characterId);
        if (index < 0)
            return false;

        _members.RemoveAt(index);
        return true;
    }

    public bool TryResolveByName(string avatarName, out int characterId)
    {
        foreach (var member in _members)
            if (string.Equals(member.Name, avatarName, StringComparison.OrdinalIgnoreCase))
            {
                characterId = member.CharacterId;
                return true;
            }

        characterId = 0;
        return false;
    }

    public int[] MemberIds()
    {
        var ids = new int[_members.Count];
        for (var i = 0; i < ids.Length; i++)
            ids[i] = _members[i].CharacterId;

        return ids;
    }

    private int IndexOf(int characterId)
    {
        for (var i = 0; i < _members.Count; i++)
            if (_members[i].CharacterId == characterId)
                return i;

        return -1;
    }
}

public sealed class PartyRegistry
{
    public const int MaxMembers = 5;

    public const int MaxLevelGap = 9;

    private static readonly TimeSpan PartyResyncRequestLifetime = TimeSpan.FromSeconds(30);

    private readonly CrossShardNegotiationTracker _crossShard = new();

    private readonly Dictionary<int, int> _leaderByMember = new();

    private readonly Lock _lock = new();

    private readonly Dictionary<int, Party> _partiesByLeader = new();

    private readonly Dictionary<int, int> _pendingByInvitee = new();

    private readonly Dictionary<int, int> _pendingByInviter = new();

    private readonly Dictionary<int, PartyResyncRequest> _pendingResyncByCharacter = new();

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
            return TryGetPartyLocked(characterId, out var party) ? party.MemberIds() : [];
        }
    }

    public IReadOnlyList<PartyMember> GetRoster(int characterId)
    {
        lock (_lock)
        {
            return TryGetPartyLocked(characterId, out var party) ? party.Members.ToArray() : [];
        }
    }

    public bool TryGetMemberName(int characterId, out string name)
    {
        lock (_lock)
        {
            if (TryGetPartyLocked(characterId, out var party))
                foreach (var member in party.Members)
                    if (member.CharacterId == characterId && member.Name.Length > 0)
                    {
                        name = member.Name;
                        return true;
                    }

            name = "";
            return false;
        }
    }

    public bool TryResolveMemberByName(int anchorCharacterId, string avatarName, out int characterId)
    {
        characterId = 0;

        if (string.IsNullOrEmpty(avatarName))
            return false;

        lock (_lock)
        {
            return TryGetPartyLocked(anchorCharacterId, out var party) &&
                   party.TryResolveByName(avatarName, out characterId);
        }
    }

    public bool TryAdoptRoster(IReadOnlyList<PartyMember> roster, int expectedMemberId,
        out IReadOnlyList<int> evictedMemberIds)
    {
        evictedMemberIds = [];

        if (!IsWellFormedRoster(roster))
            return false;

        lock (_lock)
        {
            var leaderId = roster[0].CharacterId;
            var carries = false;

            foreach (var member in roster)
                if (member.CharacterId == expectedMemberId)
                {
                    carries = true;
                    break;
                }

            if (!carries)
                return false;

            foreach (var member in roster)
                if (_leaderByMember.ContainsKey(member.CharacterId))
                    return false;

            RegisterRosterLocked(leaderId, roster);

            return true;
        }
    }

    public bool TryRegisterResyncRequest(int characterId, string avatarName, Guid correlationId)
    {
        if (characterId <= 0 || string.IsNullOrWhiteSpace(avatarName) || correlationId == Guid.Empty)
            return false;

        lock (_lock)
        {
            RemoveExpiredResyncRequestsLocked(DateTime.UtcNow);

            if (_leaderByMember.ContainsKey(characterId) || _pendingResyncByCharacter.ContainsKey(characterId))
                return false;

            _pendingResyncByCharacter[characterId] = new PartyResyncRequest(correlationId, avatarName,
                DateTime.UtcNow + PartyResyncRequestLifetime);
            return true;
        }
    }

    public void CancelResyncRequest(int characterId, Guid correlationId)
    {
        lock (_lock)
        {
            if (_pendingResyncByCharacter.TryGetValue(characterId, out var pending) &&
                pending.CorrelationId == correlationId)
                _pendingResyncByCharacter.Remove(characterId);
        }
    }

    public bool TryApplyResyncRoster(int recipientCharacterId, string recipientAvatarName, Guid correlationId,
        IReadOnlyList<PartyMember> roster)
    {
        if (!IsWellFormedRoster(roster) || correlationId == Guid.Empty)
            return false;

        lock (_lock)
        {
            RemoveExpiredResyncRequestsLocked(DateTime.UtcNow);

            if (!_pendingResyncByCharacter.TryGetValue(recipientCharacterId, out var pending) ||
                pending.CorrelationId != correlationId ||
                !string.Equals(pending.AvatarName, recipientAvatarName, StringComparison.OrdinalIgnoreCase) ||
                _leaderByMember.ContainsKey(recipientCharacterId))
                return false;

            var carriesRecipient = false;
            foreach (var member in roster)
            {
                if (member.CharacterId == recipientCharacterId &&
                    string.Equals(member.Name, recipientAvatarName, StringComparison.OrdinalIgnoreCase))
                    carriesRecipient = true;

                if (_leaderByMember.ContainsKey(member.CharacterId))
                    return false;
            }

            if (!carriesRecipient)
                return false;

            RegisterRosterLocked(roster[0].CharacterId, roster);
            _pendingResyncByCharacter.Remove(recipientCharacterId);
            return true;
        }
    }

    private static bool IsWellFormedRoster(IReadOnlyList<PartyMember> roster)
    {
        if (roster.Count is < 2 or > MaxMembers)
            return false;

        var memberIds = new HashSet<int>();
        var memberNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var member in roster)
            if (member.CharacterId <= 0 || string.IsNullOrWhiteSpace(member.Name) ||
                !memberIds.Add(member.CharacterId) || !memberNames.Add(member.Name))
                return false;

        return true;
    }

    private void RegisterRosterLocked(int leaderId, IReadOnlyList<PartyMember> roster)
    {
        var party = new Party(leaderId, [..roster]);
        _partiesByLeader[leaderId] = party;

        foreach (var member in roster)
            _leaderByMember[member.CharacterId] = leaderId;
    }

    private void RemoveExpiredResyncRequestsLocked(DateTime nowUtc)
    {
        List<int>? expiredCharacterIds = null;
        foreach (var entry in _pendingResyncByCharacter)
            if (entry.Value.ExpiresAtUtc <= nowUtc)
                (expiredCharacterIds ??= []).Add(entry.Key);

        if (expiredCharacterIds is not null)
            foreach (var characterId in expiredCharacterIds)
                _pendingResyncByCharacter.Remove(characterId);
    }

    public bool TryRemoveMemberByName(int anchorCharacterId, string avatarName, out int removedCharacterId)
    {
        removedCharacterId = 0;

        if (string.IsNullOrEmpty(avatarName))
            return false;

        lock (_lock)
        {
            if (!TryGetPartyLocked(anchorCharacterId, out var party) ||
                !party.TryResolveByName(avatarName, out var targetId) ||
                !party.TryRemoveMember(targetId))
                return false;

            _leaderByMember.Remove(targetId);
            removedCharacterId = targetId;
            return true;
        }
    }

    public IReadOnlyList<int> DisbandFor(int characterId)
    {
        lock (_lock)
        {
            if (!TryGetPartyLocked(characterId, out var party))
                return [];

            var members = party.MemberIds();
            DisbandLocked(party);
            return members;
        }
    }

    private bool TryGetPartyLocked(int characterId, [NotNullWhen(true)] out Party? party)
    {
        if (_leaderByMember.TryGetValue(characterId, out var leaderId))
            return _partiesByLeader.TryGetValue(leaderId, out party);

        party = null;
        return false;
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
        if (ask.TargetCharacterId == inviterId)
            return PartyInviteOutcome.TargetBusy;

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
        if (inviterId == inviteeId)
            return PartyInviteOutcome.TargetBusy;

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

    public bool TryCancel(int inviterId, out int inviteeId, out CrossShardOutboundAsk? crossShardAsk)
    {
        lock (_lock)
        {
            crossShardAsk = null;

            if (_pendingByInviter.Remove(inviterId, out inviteeId))
                return true;

            if (_crossShard.TryConsumeOutbound(inviterId, out var outboundAsk))
            {
                inviteeId = outboundAsk.TargetCharacterId;
                crossShardAsk = outboundAsk;
                return true;
            }

            return false;
        }
    }

    public bool TryCancel(int inviterId, out int inviteeId)
    {
        return TryCancel(inviterId, out inviteeId, out _);
    }

    public bool ClearInviteeAfterCancel(int inviteeId, int inviterId)
    {
        lock (_lock)
        {
            return _pendingByInvitee.TryGetValue(inviteeId, out var recordedInviterId) &&
                   recordedInviterId == inviterId &&
                   _pendingByInvitee.Remove(inviteeId);
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

    public bool TryConsumeCrossShardOutbound(int inviterId, byte inviteeShardId, int inviteeId,
        out CrossShardOutboundAsk ask)
    {
        lock (_lock)
        {
            return _crossShard.TryConsumeOutbound(inviterId, inviteeShardId, inviteeId, out ask);
        }
    }

    public bool TryClearCrossShardInbound(int inviteeId, byte inviterShardId, int inviterId)
    {
        lock (_lock)
        {
            return _crossShard.TryClearInbound(inviteeId, inviterShardId, inviterId);
        }
    }

    public PartyJoinOutcome TryCompleteCrossShardAnswer(PartyMember inviter, PartyMember invitee,
        out IReadOnlyList<PartyMember> members)
    {
        lock (_lock)
        {
            if (_partiesByLeader.TryGetValue(inviter.CharacterId, out var existing))
            {
                if (!existing.TryAddMember(invitee))
                {
                    members = existing.Members.ToArray();
                    return PartyJoinOutcome.PartyWasFull;
                }

                _leaderByMember[invitee.CharacterId] = inviter.CharacterId;
                members = existing.Members.ToArray();
                return PartyJoinOutcome.Joined;
            }

            var party = new Party(inviter, invitee);
            _partiesByLeader[inviter.CharacterId] = party;
            _leaderByMember[inviter.CharacterId] = inviter.CharacterId;
            _leaderByMember[invitee.CharacterId] = inviter.CharacterId;
            members = party.Members.ToArray();
            return PartyJoinOutcome.Created;
        }
    }

    public bool TryAnswer(int inviteeId, string inviteeName, string inviterName, bool accepted,
        bool inviterBusyByZoneTransfer, out int inviterId, out PartyJoinOutcome joinOutcome, out bool guardBlocked)
    {
        joinOutcome = default;
        guardBlocked = false;

        lock (_lock)
        {
            if (!_pendingByInvitee.TryGetValue(inviteeId, out inviterId))
                return false;

            if (!_pendingByInviter.TryGetValue(inviterId, out var recordedInviteeId) ||
                recordedInviteeId != inviteeId)
            {
                _pendingByInvitee.Remove(inviteeId);
                return false;
            }

            if (!accepted)
            {
                _pendingByInvitee.Remove(inviteeId);

                if (inviterBusyByZoneTransfer)
                {
                    guardBlocked = true;
                    return false;
                }

                _pendingByInviter.Remove(inviterId);
                return true;
            }

            if (inviterBusyByZoneTransfer)
            {
                guardBlocked = true;
                return false;
            }

            _pendingByInvitee.Remove(inviteeId);
            _pendingByInviter.Remove(inviterId);

            if (_partiesByLeader.TryGetValue(inviterId, out var existing))
            {
                if (!existing.TryAddMember(new PartyMember(inviteeId, inviteeName)))
                {
                    joinOutcome = PartyJoinOutcome.PartyWasFull;
                    return true;
                }

                _leaderByMember[inviteeId] = inviterId;
                joinOutcome = PartyJoinOutcome.Joined;
                return true;
            }

            var party = new Party(new PartyMember(inviterId, inviterName), new PartyMember(inviteeId, inviteeName));
            _partiesByLeader[inviterId] = party;
            _leaderByMember[inviterId] = inviterId;
            _leaderByMember[inviteeId] = inviterId;
            joinOutcome = PartyJoinOutcome.Created;
            return true;
        }
    }

    public bool TryLeave(int characterId, out IReadOnlyList<PartyMember> membersBeforeLeave, out bool disbanded)
    {
        return TryRemove(characterId, characterId, false, out membersBeforeLeave, out disbanded);
    }

    public bool TryKick(int leaderId, int targetId, out IReadOnlyList<PartyMember> membersBeforeKick,
        out bool disbanded)
    {
        return TryRemove(leaderId, targetId, true, out membersBeforeKick, out disbanded);
    }

    private bool TryRemove(int actingId, int targetId, bool requireLeader,
        out IReadOnlyList<PartyMember> membersBefore, out bool disbanded)
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
            return true;
        }
    }

    public IReadOnlyList<PartyMember> Disband(int leaderId)
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
                var members = party.MemberIds();
                DisbandLocked(party);
                return new PartyDisconnectResult(PartyDisconnectKind.LeaderDisbanded, members, []);
            }

            var membersBeforeLeave = party.MemberIds();

            if (!party.TryRemoveMember(characterId))
                return
                    PartyDisconnectResult
                        .NotInParty;

            _leaderByMember.Remove(characterId);

            return new PartyDisconnectResult(PartyDisconnectKind.MemberLeft, membersBeforeLeave, party.MemberIds());
        }
    }

    public void ClearForWorldEntry(int characterId)
    {
        lock (_lock)
        {
            if (_pendingByInviter.Remove(characterId, out var pendingInvitee))
                RemoveMirror(_pendingByInvitee, pendingInvitee, characterId);
            if (_pendingByInvitee.Remove(characterId, out var pendingInviter))
                RemoveMirror(_pendingByInviter, pendingInviter, characterId);

            _crossShard.ClearForCharacter(characterId);
        }
    }

    private static void RemoveMirror(Dictionary<int, int> map, int counterpartId, int expectedValue)
    {
        if (map.TryGetValue(counterpartId, out var mirror) && mirror == expectedValue)
            map.Remove(counterpartId);
    }

    private void DisbandLocked(Party party)
    {
        foreach (var member in party.Members)
            _leaderByMember.Remove(member.CharacterId);

        _partiesByLeader.Remove(party.LeaderId);
    }

    private readonly record struct PartyResyncRequest(Guid CorrelationId, string AvatarName, DateTime ExpiresAtUtc);
}
