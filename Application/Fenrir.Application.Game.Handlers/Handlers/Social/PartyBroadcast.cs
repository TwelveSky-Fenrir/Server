using Fenrir.Application.Game.Domain.World;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Handlers.Social;

internal static class PartyBroadcast
{

        public static PartyRosterResponse BuildRoster(ZoneRegistry zones, int sort, IReadOnlyList<int> memberIds)
    {
        Span<string> names = ["", "", "", "", ""];
        for (var i = 0; i < memberIds.Count && i < 5; i++)
            if (zones.TryGetPlayer(memberIds[i], out var member))
                names[i] = member.Name;

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
}
