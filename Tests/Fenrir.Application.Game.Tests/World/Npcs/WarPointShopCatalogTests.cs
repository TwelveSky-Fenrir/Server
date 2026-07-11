using System.Collections.Immutable;
using Fenrir.Application.Game.Domain.World.Npcs;

namespace Fenrir.Application.Game.Tests.World.Npcs;

public class WarPointShopCatalogTests
{
    [Theory]
    [InlineData(WarPointShopCatalog.NangimNpcId)]
    [InlineData(WarPointShopCatalog.NobleDragonNpcId)]
    [InlineData(WarPointShopCatalog.RoyalSerpentNpcId)]
    [InlineData(WarPointShopCatalog.GrandTigerNpcId)]
    public void IsWarPointNpc_TrueForTheFourWarPointNpcs(int npcId)
    {
        Assert.True(WarPointShopCatalog.IsWarPointNpc(npcId));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(51)]
    [InlineData(53)]
    [InlineData(103)]
    public void IsWarPointNpc_FalseForOrdinaryNpcs(int npcId)
    {
        Assert.False(WarPointShopCatalog.IsWarPointNpc(npcId));
    }

    [Fact]
    public void WarPointNpcIds_ContainsExactlyTheFourCodes()
    {
        Assert.Equal(4, WarPointShopCatalog.WarPointNpcIds.Count);
        Assert.Contains(52, (IEnumerable<int>)WarPointShopCatalog.WarPointNpcIds);
        Assert.Contains(102, (IEnumerable<int>)WarPointShopCatalog.WarPointNpcIds);
        Assert.Contains(202, (IEnumerable<int>)WarPointShopCatalog.WarPointNpcIds);
        Assert.Contains(302, (IEnumerable<int>)WarPointShopCatalog.WarPointNpcIds);
    }

    [Fact]
    public void PageAndSlotDimensions_MatchLegacyConstants()
    {
        Assert.Equal(3, WarPointShopCatalog.MaxPageCount);
        Assert.Equal(28, WarPointShopCatalog.MaxSlotPerPage);
    }

    [Fact]
    public void TryGetPrice_HitAndMiss()
    {
        var catalog = new WarPointShopCatalog([
            new WarPointPriceEntry(90200, 500, 0, [102]),
            new WarPointPriceEntry(90210, 750, 0, [102, 202, 302, 52])
        ]);

        Assert.True(catalog.TryGetPrice(90200, out var hit));
        Assert.Equal(500, hit.WarPointPrice);
        Assert.Equal(0, hit.ContributionPointPrice);

        Assert.False(catalog.TryGetPrice(90999, out _));
        Assert.Equal(2, catalog.Count);
    }

    [Fact]
    public void DisplaysAtNpc_Page2TribeEntry_OnlyOwningNpc()
    {
        var entry = new WarPointPriceEntry(90200, 500, 0, [WarPointShopCatalog.NobleDragonNpcId]);

        Assert.True(entry.DisplaysAtNpc(WarPointShopCatalog.NobleDragonNpcId));
        Assert.False(entry.DisplaysAtNpc(WarPointShopCatalog.RoyalSerpentNpcId));
        Assert.False(entry.DisplaysAtNpc(WarPointShopCatalog.NangimNpcId));
    }

    [Fact]
    public void DisplaysAtNpc_Page1Or3Entry_AllFourNpcs()
    {
        var entry = new WarPointPriceEntry(90210, 750, 0,
        [
            WarPointShopCatalog.NangimNpcId, WarPointShopCatalog.NobleDragonNpcId,
            WarPointShopCatalog.RoyalSerpentNpcId, WarPointShopCatalog.GrandTigerNpcId
        ]);

        Assert.True(entry.DisplaysAtNpc(WarPointShopCatalog.NangimNpcId));
        Assert.True(entry.DisplaysAtNpc(WarPointShopCatalog.GrandTigerNpcId));
    }

    [Fact]
    public void DisplaysAtNpc_DefaultOrEmptyDisplaySet_NeverDisplays()
    {
        var defaulted = new WarPointPriceEntry(1, 1, 0, default);
        Assert.False(defaulted.DisplaysAtNpc(WarPointShopCatalog.NobleDragonNpcId));

        var empty = new WarPointPriceEntry(1, 1, 0, ImmutableArray<int>.Empty);
        Assert.False(empty.DisplaysAtNpc(WarPointShopCatalog.NobleDragonNpcId));
    }

    [Fact]
    public void ProductionCatalog_Has48Rows_TheVerbatimLiveCatalogueSize()
    {
        Assert.Equal(48, WarPointShopCatalog.Production.Count);
    }

    [Theory]
    [InlineData(8101, 200)]
    [InlineData(8102, 200)]
    [InlineData(8106, 200)]
    [InlineData(2397, 50)]
    [InlineData(1103, 250)]
    [InlineData(8408, 0)]
    [InlineData(8407, 0)]
    [InlineData(8406, 50)]
    [InlineData(1126, 200)]
    [InlineData(1243, 150)]
    public void ProductionCatalog_Page0Rows_PricedAndDisplayedAtAllFourNpcs(int itemId, int expectedWarPointPrice)
    {
        Assert.True(WarPointShopCatalog.Production.TryGetPrice(itemId, out var entry));
        Assert.Equal(expectedWarPointPrice, entry.WarPointPrice);
        Assert.Equal(0, entry.ContributionPointPrice);
        Assert.True(entry.DisplaysAtNpc(WarPointShopCatalog.NangimNpcId));
        Assert.True(entry.DisplaysAtNpc(WarPointShopCatalog.NobleDragonNpcId));
        Assert.True(entry.DisplaysAtNpc(WarPointShopCatalog.RoyalSerpentNpcId));
        Assert.True(entry.DisplaysAtNpc(WarPointShopCatalog.GrandTigerNpcId));
    }

    [Theory]
    [InlineData(86700)]
    [InlineData(86703)]
    [InlineData(86723)]
    public void ProductionCatalog_Page2Rows_ZeroPricedAndDisplayedAtAllFourNpcs(int itemId)
    {
        Assert.True(WarPointShopCatalog.Production.TryGetPrice(itemId, out var entry));
        Assert.Equal(0, entry.WarPointPrice);
        Assert.Equal(0, entry.ContributionPointPrice);
        Assert.True(entry.DisplaysAtNpc(WarPointShopCatalog.NangimNpcId));
        Assert.True(entry.DisplaysAtNpc(WarPointShopCatalog.GrandTigerNpcId));
    }

    [Fact]
    public void ProductionCatalog_Page2DeadRows_AreNotPresent()
    {
        Assert.False(WarPointShopCatalog.Production.TryGetPrice(86725, out _));
        Assert.False(WarPointShopCatalog.Production.TryGetPrice(90786, out _));
    }

    [Theory]
    [InlineData(87077, WarPointShopCatalog.NobleDragonNpcId)]
    [InlineData(87084, WarPointShopCatalog.NobleDragonNpcId)]
    [InlineData(87099, WarPointShopCatalog.RoyalSerpentNpcId)]
    [InlineData(87106, WarPointShopCatalog.RoyalSerpentNpcId)]
    [InlineData(87121, WarPointShopCatalog.GrandTigerNpcId)]
    [InlineData(87128, WarPointShopCatalog.GrandTigerNpcId)]
    public void ProductionCatalog_Page1TribeRows_PricedAt11570WpAndDisplayedOnlyAtOwningNpc(int itemId,
        int owningNpcId)
    {
        Assert.True(WarPointShopCatalog.Production.TryGetPrice(itemId, out var entry));
        Assert.Equal(11570, entry.WarPointPrice);
        Assert.Equal(0, entry.ContributionPointPrice);
        Assert.True(entry.DisplaysAtNpc(owningNpcId));
        Assert.False(entry.DisplaysAtNpc(WarPointShopCatalog.NangimNpcId));

        foreach (var otherNpcId in new[]
                 {
                     WarPointShopCatalog.NobleDragonNpcId, WarPointShopCatalog.RoyalSerpentNpcId,
                     WarPointShopCatalog.GrandTigerNpcId
                 })
            if (otherNpcId != owningNpcId)
                Assert.False(entry.DisplaysAtNpc(otherNpcId));
    }

    [Fact]
    public void ProductionCatalog_NangimNeverDisplaysAnyPage1TribeRow()
    {
        foreach (var itemId in new[] { 87077, 87099, 87121 })
        {
            Assert.True(WarPointShopCatalog.Production.TryGetPrice(itemId, out var entry));
            Assert.False(entry.DisplaysAtNpc(WarPointShopCatalog.NangimNpcId));
        }
    }
}
