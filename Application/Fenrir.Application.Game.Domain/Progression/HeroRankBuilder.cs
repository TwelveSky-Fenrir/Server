using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Progression;

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
