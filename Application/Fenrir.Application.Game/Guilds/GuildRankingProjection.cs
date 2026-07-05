using System.Collections.Immutable;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Data.Guilds;

namespace Fenrir.Application.Game.Guilds;

/// <summary>Projects <see cref="GuildRankingCache" />'s snapshot onto WorldInfo's ranking-board fields.</summary>
public static class GuildRankingProjection
{
    /// <summary>
    ///     Returns <paramref name="template" /> with GuildName3/GuildScore overlaid from <paramref name="top" />
    ///     (highest points first, index 0). Every other WorldInfo field is passed through unchanged -- callers
    ///     supply <c>WorldStateTemplates.ZeroedWorldInfo</c> as the template until those fields gain their own
    ///     backing state.
    /// </summary>
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
