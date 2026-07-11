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

        private static readonly ImmutableArray<int> NobleDragonOnly = [NobleDragonNpcId];

        private static readonly ImmutableArray<int> RoyalSerpentOnly = [RoyalSerpentNpcId];

        private static readonly ImmutableArray<int> GrandTigerOnly = [GrandTigerNpcId];

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
            new WarPointPriceEntry(8101, 200, 0, AllFourNpcs),
            new WarPointPriceEntry(8102, 200, 0, AllFourNpcs),
            new WarPointPriceEntry(8106, 200, 0, AllFourNpcs),
            new WarPointPriceEntry(2397, 50, 0, AllFourNpcs),
            new WarPointPriceEntry(1103, 250, 0, AllFourNpcs),
            new WarPointPriceEntry(8408, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(8407, 0, 0, AllFourNpcs),
            new WarPointPriceEntry(8406, 50, 0, AllFourNpcs),
            new WarPointPriceEntry(1126, 200, 0, AllFourNpcs),
            new WarPointPriceEntry(1243, 150, 0, AllFourNpcs),

            new WarPointPriceEntry(87077, 11570, 0, NobleDragonOnly),
            new WarPointPriceEntry(87078, 11570, 0, NobleDragonOnly),
            new WarPointPriceEntry(87079, 11570, 0, NobleDragonOnly),
            new WarPointPriceEntry(87080, 11570, 0, NobleDragonOnly),
            new WarPointPriceEntry(87081, 11570, 0, NobleDragonOnly),
            new WarPointPriceEntry(87082, 11570, 0, NobleDragonOnly),
            new WarPointPriceEntry(87083, 11570, 0, NobleDragonOnly),
            new WarPointPriceEntry(87084, 11570, 0, NobleDragonOnly),
            new WarPointPriceEntry(87099, 11570, 0, RoyalSerpentOnly),
            new WarPointPriceEntry(87100, 11570, 0, RoyalSerpentOnly),
            new WarPointPriceEntry(87101, 11570, 0, RoyalSerpentOnly),
            new WarPointPriceEntry(87102, 11570, 0, RoyalSerpentOnly),
            new WarPointPriceEntry(87103, 11570, 0, RoyalSerpentOnly),
            new WarPointPriceEntry(87104, 11570, 0, RoyalSerpentOnly),
            new WarPointPriceEntry(87105, 11570, 0, RoyalSerpentOnly),
            new WarPointPriceEntry(87106, 11570, 0, RoyalSerpentOnly),
            new WarPointPriceEntry(87121, 11570, 0, GrandTigerOnly),
            new WarPointPriceEntry(87122, 11570, 0, GrandTigerOnly),
            new WarPointPriceEntry(87123, 11570, 0, GrandTigerOnly),
            new WarPointPriceEntry(87124, 11570, 0, GrandTigerOnly),
            new WarPointPriceEntry(87125, 11570, 0, GrandTigerOnly),
            new WarPointPriceEntry(87126, 11570, 0, GrandTigerOnly),
            new WarPointPriceEntry(87127, 11570, 0, GrandTigerOnly),
            new WarPointPriceEntry(87128, 11570, 0, GrandTigerOnly),

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
