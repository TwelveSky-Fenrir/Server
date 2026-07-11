using Fenrir.Application.Game.Domain.Inventory;

namespace Fenrir.Application.Game.Tests.Inventory;

public class EliteDropNoticeResolverTests
{
    [Theory]
    [InlineData(GroundDropOrigin.InventoryToWorld)]
    [InlineData(GroundDropOrigin.GmToWorld)]
    [InlineData(GroundDropOrigin.LevelBonusToWorld)]
    [InlineData(GroundDropOrigin.BuyTribeItemToWorld)]
    public void NonQualifyingOrigin_NeverAnnounces(GroundDropOrigin origin)
    {
        Assert.Null(EliteDropNoticeResolver.Resolve(origin, 1145, 2, "Hero"));
    }

    [Fact]
    public void MonsterToWorld_OrdinaryItem_NeverAnnounces_EliteBlockCommentedOut()
    {
        Assert.Null(EliteDropNoticeResolver.Resolve(GroundDropOrigin.MonsterToWorld, 1145, 2, "Hero"));
    }

    [Fact]
    public void MonsterToWorld_Pet_NeverAnnounces_PetsAreTreasureChestOnly()
    {
        Assert.Null(EliteDropNoticeResolver.Resolve(GroundDropOrigin.MonsterToWorld, 1003, 2, "Hero"));
    }

    [Fact]
    public void CpExchangeToWorld_OrdinaryItem_NeverAnnounces()
    {
        Assert.Null(EliteDropNoticeResolver.Resolve(GroundDropOrigin.CpExchangeToWorld, 8109, 1, "Hero"));
    }

    [Theory]
    [InlineData(1002)]
    [InlineData(1003)]
    [InlineData(1004)]
    [InlineData(1005)]
    public void TreasureChest_Pet_Announces_WithType55(int petItemId)
    {
        var notice = EliteDropNoticeResolver.Resolve(GroundDropOrigin.TreasureChestToWorld, petItemId, 3, "Chesty");

        Assert.NotNull(notice);
        Assert.Equal(EliteDropNotice.TypeTreasureChest, notice!.Value.Type);
        Assert.Equal(petItemId, notice.Value.Value);
        Assert.Equal(3, notice.Value.Tribe);
        Assert.Equal("Chesty", notice.Value.AvatarName);
    }

    [Fact]
    public void TreasureChest175_Pet_Announces_WithType56()
    {
        var notice = EliteDropNoticeResolver.Resolve(GroundDropOrigin.TreasureChest175ToWorld, 1005, 0, "Hero");

        Assert.NotNull(notice);
        Assert.Equal(EliteDropNotice.TypeTreasureChest175, notice!.Value.Type);
    }

    [Fact]
    public void TreasureChest_OrdinaryItem_NeverAnnounces()
    {
        Assert.Null(EliteDropNoticeResolver.Resolve(GroundDropOrigin.TreasureChestToWorld, 1145, 3, "Chesty"));
    }

    [Fact]
    public void PvpToWorld_OrdinaryItem_AlwaysAnnounces_WithType2()
    {
        var notice = EliteDropNoticeResolver.Resolve(GroundDropOrigin.PvpToWorld, 864, 1, "Slayer");

        Assert.NotNull(notice);
        Assert.Equal(EliteDropNotice.TypePvp, notice!.Value.Type);
        Assert.Equal(864, notice.Value.Value);
    }

    [Fact]
    public void PvpToWorld_Pet_AlsoAnnounces_PvpForceOverridesTreasureChestOnlyPetRule()
    {
        var notice = EliteDropNoticeResolver.Resolve(GroundDropOrigin.PvpToWorld, 1002, 1, "Slayer");

        Assert.NotNull(notice);
        Assert.Equal(EliteDropNotice.TypePvp, notice!.Value.Type);
    }
}
