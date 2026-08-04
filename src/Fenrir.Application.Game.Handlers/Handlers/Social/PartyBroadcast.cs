using Fenrir.Application.Game.Domain.Social.Party;
using Fenrir.Application.Game.Domain.World;
using Fenrir.Protocol.Game;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

internal static class PartyBroadcast
{
    public static PartyRosterResponse BuildRoster(int sort, IReadOnlyList<PartyMember> members)
    {
        Span<string> names = ["", "", "", "", ""];
        for (var i = 0; i < members.Count && i < 5; i++)
            names[i] = members[i].Name;

        return new PartyRosterResponse
        {
            Sort = sort,
            AvatarName01 = names[0],
            AvatarName02 = names[1],
            AvatarName03 = names[2],
            AvatarName04 = names[3],
            AvatarName05 = names[4]
        };
    }

    public static void SendOrRelayNotice<TPacket>(ZoneRegistry zones, IPartyResyncRelayQueue relay,
        byte shardId, int memberId, in TPacket localPacket, PartyResyncRelaySort remoteSort, string actorAvatarName)
        where TPacket : struct, IOutgoingPacket
    {
        if (zones.TryGetPlayer(memberId, out var member))
        {
            member.Session.Send(localPacket);
            return;
        }

        relay.Enqueue(new PartyResyncRelayEntry((byte)remoteSort, shardId, memberId, "", actorAvatarName));
    }

    public static void SendOrRelayRoster(ZoneRegistry zones, IPartyResyncRelayQueue relay, byte shardId,
        PartyMember recipient, int sort, IReadOnlyList<PartyMember> members)
    {
        var roster = BuildRoster(sort, members);

        if (zones.TryGetPlayer(recipient.CharacterId, out var localRecipient))
        {
            localRecipient.Session.Send(roster);
            return;
        }

        relay.Enqueue(new PartyResyncRelayEntry(
            (byte)PartyResyncRelaySort.PartyInfoReply,
            shardId,
            recipient.CharacterId,
            members[0].Name,
            recipient.Name)
        {
            MemberId1 = MemberIdAt(members, 0),
            MemberName1 = MemberNameAt(members, 0),
            MemberId2 = MemberIdAt(members, 1),
            MemberName2 = MemberNameAt(members, 1),
            MemberId3 = MemberIdAt(members, 2),
            MemberName3 = MemberNameAt(members, 2),
            MemberId4 = MemberIdAt(members, 3),
            MemberName4 = MemberNameAt(members, 3),
            MemberId5 = MemberIdAt(members, 4),
            MemberName5 = MemberNameAt(members, 4)
        });
    }

    private static int MemberIdAt(IReadOnlyList<PartyMember> members, int index)
    {
        return index < members.Count ? members[index].CharacterId : 0;
    }

    private static string MemberNameAt(IReadOnlyList<PartyMember> members, int index)
    {
        return index < members.Count ? members[index].Name : "";
    }
}
