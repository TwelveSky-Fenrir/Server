using Fenrir.Application.Game.World;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Application.Game.Handlers.Social;

/// <summary>Shared ZC_PARTY_MAKE_INFO (opcode 74) roster builder.</summary>
internal static class PartyBroadcast
{
    /// <summary>
    ///     5 name slots, leader first (<paramref name="memberIds" />[0]); resolved live since a member could be in any
    ///     zone.
    /// </summary>
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
