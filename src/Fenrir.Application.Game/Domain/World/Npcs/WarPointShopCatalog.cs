using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Npcs;

public sealed class WarPointShopCatalog
{
    public const int NangimNpcId = 52;

    public const int NobleDragonNpcId = 102;

    public const int RoyalSerpentNpcId = 202;

    public const int GrandTigerNpcId = 302;

    public const int MaxPageCount = 3;

    public const int MaxSlotPerPage = 28;

    public static readonly FrozenSet<int> WarPointNpcIds =
        new[] { NangimNpcId, NobleDragonNpcId, RoyalSerpentNpcId, GrandTigerNpcId }.ToFrozenSet();

    private static readonly ImmutableArray<int> AllFourNpcs =
        [NangimNpcId, NobleDragonNpcId, RoyalSerpentNpcId, GrandTigerNpcId];

    private readonly FrozenDictionary<int, WarPointPriceEntry> _pricesByItemId;

    public WarPointShopCatalog(IEnumerable<WarPointPriceEntry> entries)
    {
        _pricesByItemId = entries.ToFrozenDictionary(entry => entry.ItemId);
    }

    public static WarPointShopCatalog Production { get; } = new(BuildProductionEntries());

    public int Count => _pricesByItemId.Count;

    public static bool IsWarPointNpc(int npcId)
    {
        return WarPointNpcIds.Contains(npcId);
    }

    public bool TryGetPrice(int itemId, out WarPointPriceEntry entry)
    {
        return _pricesByItemId.TryGetValue(itemId, out entry);
    }

    private static IReadOnlyList<WarPointPriceEntry> BuildProductionEntries()
    {
        return
        [
            new WarPointPriceEntry(8101, 10, 0, AllFourNpcs),
            new WarPointPriceEntry(8102, 10, 0, AllFourNpcs),
            new WarPointPriceEntry(8106, 10, 0, AllFourNpcs),
            new WarPointPriceEntry(2397, 10, 0, AllFourNpcs),
            new WarPointPriceEntry(1103, 5, 0, AllFourNpcs),
            new WarPointPriceEntry(8408, 50, 0, AllFourNpcs),
            new WarPointPriceEntry(8407, 50, 0, AllFourNpcs),
            new WarPointPriceEntry(8406, 50, 0, AllFourNpcs),
            new WarPointPriceEntry(1126, 5, 0, AllFourNpcs),
            new WarPointPriceEntry(1243, 10, 0, AllFourNpcs),

            new WarPointPriceEntry(15135, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(15157, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(15179, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(15201, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(15223, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(15245, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(15267, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(15289, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(35135, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(35157, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(35179, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(35201, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(35223, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(35245, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(35267, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(35289, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(55135, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(55157, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(55179, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(55201, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(55223, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(55245, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(55267, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(55289, 0, 0, AllFourNpcs),

            new WarPointPriceEntry(86700, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86703, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86704, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86705, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86707, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86709, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86712, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86713, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86714, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86716, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86718, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86721, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86722, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(86723, 0, 0, AllFourNpcs)
        ];
    }
}

public readonly record struct WarPointPriceEntry(
    int ItemId,
    int WarPointPrice,
    int ContributionPointPrice,
    ImmutableArray<int> DisplayNpcIds)
{
    public bool DisplaysAtNpc(int npcId)
    {
        return !DisplayNpcIds.IsDefaultOrEmpty && DisplayNpcIds.Contains(npcId);
    }
}
