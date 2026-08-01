using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.World.ZoneWar;

public static class KnownTSortRegistry
{
    public static readonly FrozenSet<int> KnownSorts = Build();

    // Les deux seuls tSort qui font `return` avant le fan-out global du center legacy
    // (Server/ts25center/S04_MyWork02.cpp:1189 pour 9998, :1211 pour 10000). Volontairement hors de KnownSorts.
    public static readonly FrozenSet<int> NeverFannedOutSorts = new[] { 9998, 10000 }.ToFrozenSet();

    // Produits par le center lui-meme, jamais recus d'une zone : 1234 (S07_MyGame01.cpp:262),
    // 1600 (S07_MyGame01.cpp:236), 1601 (S04_MyWork02.cpp:1154). Chaque shard a deja son propre producteur.
    public static readonly FrozenSet<int> CenterOriginatedSorts = new[] { 1234, 1600, 1601 }.ToFrozenSet();

    public static bool IsKnown(int sort)
    {
        return KnownSorts.Contains(sort);
    }

    public static bool CrossesShardBoundary(int sort)
    {
        return !NeverFannedOutSorts.Contains(sort) && !CenterOriginatedSorts.Contains(sort);
    }

    private static FrozenSet<int> Build()
    {
        var set = new HashSet<int>();

        AddRange(set, 1, 115);

        AddRange(set, 200, 208);

        set.Add(301);
        set.Add(302);

        set.Add(401);

        AddRange(set, 402, 415);

        set.Add(416);
        AddRange(set, 418, 428);
        set.Add(500);

        AddRange(set, 601, 615);

        set.Add(620);
        set.Add(621);
        set.Add(622);
        set.Add(626);
        set.Add(628);

        set.Add(659);
        set.Add(660);
        set.Add(661);
        set.Add(662);
        AddRange(set, 663, 669);
        set.Add(671);
        set.Add(672);

        set.Add(673);
        set.Add(674);
        set.Add(675);

        set.Add(751);

        set.Add(752);
        AddRange(set, 753, 763);
        AddRange(set, 771, 774);

        set.Add(1001);

        set.Add(1133);

        // Codes origine-center : 1234 traverse deja le relais inter-shard (publie par
        // FavoredTribeRankBonusLadderService), d'ou l'allowlist. Voir CenterOriginatedSorts pour la non-republication.
        set.Add(1234);
        set.Add(1600);
        set.Add(1601);

        AddRange(set, 1501, 1507);
        AddRange(set, 1510, 1514);
        set.Add(1520);

        set.Add(1611);
        set.Add(1612);
        set.Add(1614);
        set.Add(1615);

        set.Add(2000);
        set.Add(2001);

        set.Add(2002);
        set.Add(2003);

        set.Add(2101);

        set.Add(3001);

        set.Add(4000);

        return set.ToFrozenSet();
    }

    private static void AddRange(HashSet<int> set, int inclusiveLow, int inclusiveHigh)
    {
        for (var value = inclusiveLow; value <= inclusiveHigh; value++)
            set.Add(value);
    }
}
