using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Progression;

/// <summary>
///     Builds the wire <see cref="HeroRank" /> grid (flat index = tribe*10+rank) from
///     <c>usp_HeroRanking_GetByPeriod</c>'s rows, which are globally Points-DESC ordered but not
///     partitioned by tribe -- mirrors the legacy's own per-tribe "ORDER BY hPoint DESC LIMIT 10" query
///     (S08_MyDB.cpp:258/295) by taking each tribe's first 10 matches off the shared, already-sorted list.
/// </summary>
public static class HeroRankBuilder
{
    public const int TribeCount = 4;
    public const int SlotsPerTribe = 10;

    public static HeroRank Build(IReadOnlyList<HeroRankingRowDto> rowsOrderedByPointsDescending)
    {
        var names = new string[TribeCount * SlotsPerTribe];
        var points = new int[TribeCount * SlotsPerTribe];
        Array.Fill(names, string.Empty);

        Span<int> filled = stackalloc int[TribeCount];

        foreach (var row in rowsOrderedByPointsDescending)
        {
            if (row.TribeId is not ({ } tribeId and < TribeCount))
                continue;

            var rank = filled[tribeId];
            if (rank >= SlotsPerTribe)
                continue;

            var slot = tribeId * SlotsPerTribe + rank;
            names[slot] = row.CharacterName;
            points[slot] = row.Points;
            filled[tribeId] = rank + 1;
        }

        return new HeroRank { Name = names, Point = points };
    }
}
