using System.Collections.Frozen;

namespace Fenrir.Application.Game.Domain.Commerce;

public static class CostumeSearchWhitelist
{
    public const int CostumeCategory = 4;

    public static readonly FrozenSet<int> ItemIds = BuildWhitelist();

    public static bool Contains(int itemId)
    {
        return ItemIds.Contains(itemId);
    }

    private static FrozenSet<int> BuildWhitelist()
    {
        var ids = new List<int>(195);
        ids.AddRange(Enumerable.Range(301, 102));
        ids.AddRange(Enumerable.Range(2146, 3));
        ids.AddRange(Enumerable.Range(1801, 3));
        ids.AddRange(Enumerable.Range(1891, 3));
        ids.AddRange(Enumerable.Range(17701, 3));
        ids.AddRange(Enumerable.Range(18124, 9));
        ids.AddRange(Enumerable.Range(93301, 30));
        ids.AddRange(Enumerable.Range(93334, 12));
        ids.AddRange(Enumerable.Range(93376, 6));
        ids.AddRange(Enumerable.Range(93385, 21));
        ids.AddRange(Enumerable.Range(76524, 3));
        return ids.ToFrozenSet();
    }
}
