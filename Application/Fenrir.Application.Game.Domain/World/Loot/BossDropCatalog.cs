using System.Collections.Frozen;
using System.Collections.Immutable;

namespace Fenrir.Application.Game.Domain.World.Loot;

public sealed class BossDropCatalog
{

        public const int SharedRandomPoolElixirSlotIndex = 3;

    private BossDropCatalog()
    {
        NineItemEventList = Guaranteed(
        [
            (1103, 1), (601, 1), (602, 1), (2249, 1), (1073, 1), (724, 2), (1437, 1), (1422, 1), (1448, 1)
        ]);

        HolyUnicornPersonalList = Guaranteed(
        [
            (1073, 1), (698, 1), (1448, 1), (2003, 1), (724, 2), (8112, 1)
        ]);

        ThreeItemEventList = Guaranteed([(1073, 1), (1447, 1), (723, 1)]);

        EliteBossGuaranteedList = Guaranteed(
        [
            (1073, 1), (698, 1), (1448, 1), (724, 2), (2003, 1), (8112, 1), (1437, 1), (1422, 1)
        ]);

        CustomTimedBossLists = new Dictionary<int, IReadOnlyList<DroppedItem>>
        {
            [564] = Guaranteed([(8109, 1), (1166, 1), (1243, 1), (696, 1), (8102, 1), (1237, 1)]),
            [565] = Guaranteed([(1126, 1), (2001, 1), (696, 1), (92286, 1), (2477, 1), (1103, 1)]),
            [566] = Guaranteed([(8113, 1), (2001, 1), (1103, 1), (1072, 1), (1178, 1), (1179, 1)]),
            [567] = Guaranteed([(8113, 1), (2002, 1), (1103, 1), (1072, 1), (92286, 1), (92287, 1)]),
            [568] = Guaranteed([(8112, 1), (1434, 1), (8113, 1), (2002, 1), (1103, 1), (1073, 1), (828, 1)])
        }.ToFrozenDictionary();

        DemonLordItemPool = [8109, 1492, 8102, 1045, 1019, 1020, 1021, 1022, 1023, 1017, 1018, 1092, 1093];

        FifteenMinuteBossLowMidFixedIds = [1178, 92286];
        FifteenMinuteBossMidTierPool = [2249, 602, 1449, 724, 8102, 1023, 1022, 1422, 1072];
        FifteenMinuteBossHighTierPool = [695, 696, 698];

        SharedRandomPoolFixedIds = [1023, 1022, 8102, 695, 696];
    }

        public static BossDropCatalog Default { get; } = new();

        public IReadOnlyList<DroppedItem> NineItemEventList { get; }

        public IReadOnlyList<DroppedItem> HolyUnicornPersonalList { get; }

        public IReadOnlyList<DroppedItem> ThreeItemEventList { get; }

        public IReadOnlyList<DroppedItem> EliteBossGuaranteedList { get; }

        public FrozenDictionary<int, IReadOnlyList<DroppedItem>> CustomTimedBossLists { get; }

        public ImmutableArray<int> DemonLordItemPool { get; }

        public ImmutableArray<int> FifteenMinuteBossLowMidFixedIds { get; }

        public ImmutableArray<int> FifteenMinuteBossMidTierPool { get; }

        public ImmutableArray<int> FifteenMinuteBossHighTierPool { get; }

        public ImmutableArray<int> SharedRandomPoolFixedIds { get; }

    private static IReadOnlyList<DroppedItem> Guaranteed(ReadOnlySpan<(int ItemId, int Quantity)> entries)
    {
        var builder = ImmutableArray.CreateBuilder<DroppedItem>(entries.Length);
        foreach (var (itemId, quantity) in entries)
            builder.Add(new DroppedItem(itemId, quantity));

        return builder.MoveToImmutable();
    }
}
