using System.Collections.Frozen;

namespace Fenrir.Cluster.Center.EventBus;

public static class KnownTSortRegistry
{
    public static readonly FrozenSet<int> KnownSorts = Build();

    public static bool IsKnown(int sort)
    {
        return KnownSorts.Contains(sort);
    }

    private static FrozenSet<int> Build()
    {
        var set = new HashSet<int>();

        AddRange(set, 1, 115);

        AddRange(set, 200, 208);

        set.Add(301);
        set.Add(302);

        AddRange(set, 402, 415);

        set.Add(416);
        AddRange(set, 418, 428);
        set.Add(500);

        AddRange(set, 601, 615);
        set.Add(621);
        set.Add(628);
        set.Add(661);
        AddRange(set, 663, 669);
        set.Add(671);
        set.Add(672);
        set.Add(674);
        set.Add(675);

        set.Add(751);
        AddRange(set, 753, 763);
        AddRange(set, 771, 774);

        set.Add(1133);

        AddRange(set, 1501, 1507);
        AddRange(set, 1510, 1514);
        set.Add(1520);

        set.Add(4000);

        return set.ToFrozenSet();
    }

    private static void AddRange(HashSet<int> set, int inclusiveLow, int inclusiveHigh)
    {
        for (var value = inclusiveLow; value <= inclusiveHigh; value++)
            set.Add(value);
    }
}
