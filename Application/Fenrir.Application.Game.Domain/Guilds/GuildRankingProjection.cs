using System.Collections.Immutable;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Guilds;

public static class GuildRankingProjection
{

        public static WorldInfo Apply(WorldInfo template, ImmutableArray<GuildRankingRowDto> top)
    {
        var names = new string[GuildRankingCache.TopCount];
        var scores = new int[GuildRankingCache.TopCount];
        Array.Fill(names, "");

        for (var i = 0; i < names.Length && i < top.Length; i++)
        {
            names[i] = top[i].Name;
            scores[i] = top[i].Points;
        }

        return template with { GuildName3 = names, GuildScore = scores };
    }
}
