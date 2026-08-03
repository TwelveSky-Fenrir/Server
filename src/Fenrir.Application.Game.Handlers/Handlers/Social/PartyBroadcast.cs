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
}
