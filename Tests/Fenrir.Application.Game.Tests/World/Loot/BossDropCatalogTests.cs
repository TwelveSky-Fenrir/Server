using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Tests.World.Loot;

public class BossDropCatalogTests
{
    private static readonly BossDropCatalog Catalog = BossDropCatalog.Default;

    [Fact]
    public void NineItemEventList_MatchesContract_IncludingItem724AtQuantityTwo()
    {
        DroppedItem[] expected =
        [
            new(1103, 1), new(601, 1), new(602, 1), new(2249, 1), new(1073, 1),
            new(724, 2), new(1437, 1), new(1422, 1), new(1448, 1)
        ];

        Assert.Equal(expected, Catalog.NineItemEventList);
    }

    [Fact]
    public void HolyUnicornPersonalList_MatchesContract_IncludingItem724AtQuantityTwo()
    {
        DroppedItem[] expected =
        [
            new(1073, 1), new(698, 1), new(1448, 1), new(2003, 1), new(724, 2), new(8112, 1)
        ];

        Assert.Equal(expected, Catalog.HolyUnicornPersonalList);
    }

    [Fact]
    public void ThreeItemEventList_MatchesContract()
    {
        DroppedItem[] expected = [new(1073, 1), new(1447, 1), new(723, 1)];

        Assert.Equal(expected, Catalog.ThreeItemEventList);
    }

    [Fact]
    public void EliteBossGuaranteedList_MatchesContract_IncludingItem724AtQuantityTwo()
    {
        DroppedItem[] expected =
        [
            new(1073, 1), new(698, 1), new(1448, 1), new(724, 2), new(2003, 1), new(8112, 1), new(1437, 1),
            new(1422, 1)
        ];

        Assert.Equal(expected, Catalog.EliteBossGuaranteedList);
    }

    [Fact]
    public void CustomTimedBossLists_CoverExactly564Through568()
    {
        Assert.Equal([564, 565, 566, 567, 568], Catalog.CustomTimedBossLists.Keys.Order());
    }

    [Fact]
    public void CustomTimedBoss564_MatchesContract()
    {
        DroppedItem[] expected =
            [new(8109, 1), new(1166, 1), new(1243, 1), new(696, 1), new(8102, 1), new(1237, 1)];

        Assert.Equal(expected, Catalog.CustomTimedBossLists[564]);
    }

    [Fact]
    public void CustomTimedBoss568_HasSevenEntries_MatchesContract()
    {
        DroppedItem[] expected =
        [
            new(8112, 1), new(1434, 1), new(8113, 1), new(2002, 1), new(1103, 1), new(1073, 1), new(828, 1)
        ];

        Assert.Equal(expected, Catalog.CustomTimedBossLists[568]);
    }

    [Fact]
    public void DemonLordItemPool_IsTheThirteenEntryPool_InContractOrder()
    {
        int[] expected = [8109, 1492, 8102, 1045, 1019, 1020, 1021, 1022, 1023, 1017, 1018, 1092, 1093];

        Assert.Equal(expected, Catalog.DemonLordItemPool);
    }

    [Fact]
    public void FifteenMinuteBossMidTierPool_MatchesContract()
    {
        int[] expected = [2249, 602, 1449, 724, 8102, 1023, 1022, 1422, 1072];

        Assert.Equal(expected, Catalog.FifteenMinuteBossMidTierPool);
    }

    [Fact]
    public void FifteenMinuteBossHighTierPool_MatchesContract()
    {
        int[] expected = [695, 696, 698];

        Assert.Equal(expected, Catalog.FifteenMinuteBossHighTierPool);
    }

    [Fact]
    public void FifteenMinuteBossLowMidFixedIds_AreTheTwoNonAnimalEntries()
    {
        int[] expected = [1178, 92286];

        Assert.Equal(expected, Catalog.FifteenMinuteBossLowMidFixedIds);
    }

    [Fact]
    public void SharedRandomPoolFixedIds_AreTheFiveNonElixirEntries_InPoolOrder()
    {
        int[] expected = [1023, 1022, 8102, 695, 696];

        Assert.Equal(expected, Catalog.SharedRandomPoolFixedIds);
    }
}
