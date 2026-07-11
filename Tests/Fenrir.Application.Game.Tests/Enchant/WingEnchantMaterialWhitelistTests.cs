using Fenrir.Application.Game.Domain.Enchant;

namespace Fenrir.Application.Game.Tests.Enchant;

public class WingEnchantMaterialWhitelistTests
{
    [Theory]
    [InlineData(695)]
    [InlineData(696)]
    [InlineData(698)]
    [InlineData(826)]
    [InlineData(2387)]
    [InlineData(2392)]
    [InlineData(2397)]
    [InlineData(8106)]
    public void ClassWhitelist_ContainsEveryLiveGate1Id(int itemId)
    {
        Assert.Contains(itemId, (IEnumerable<int>)WingEnchantMaterialWhitelist.ClassWhitelist);
    }

    [Fact]
    public void ClassWhitelist_ExcludesDeadOnlineForDsId()
    {
        Assert.DoesNotContain(99409, (IEnumerable<int>)WingEnchantMaterialWhitelist.ClassWhitelist);
    }

    [Theory]
    [InlineData(2387)]
    [InlineData(2392)]
    public void Gate2MissingDisconnects_ContainsOnlyTheTwoConfirmedInconsistentIds(int itemId)
    {
        Assert.Contains(itemId, (IEnumerable<int>)WingEnchantMaterialWhitelist.Gate2MissingDisconnects);
        Assert.Contains(itemId, (IEnumerable<int>)WingEnchantMaterialWhitelist.ClassWhitelist);
    }

    [Fact]
    public void Gate2MissingDisconnects_HasExactlyTwoEntries()
    {
        Assert.Equal(2, WingEnchantMaterialWhitelist.Gate2MissingDisconnects.Count);
    }

    [Theory]
    [InlineData(696)]
    [InlineData(698)]
    [InlineData(2397)]
    public void WhitelistedAmountNotCited_ContainsTheThreeUnamountedIds(int itemId)
    {
        Assert.Contains(itemId, (IEnumerable<int>)WingEnchantMaterialWhitelist.WhitelistedAmountNotCited);
    }

    [Fact]
    public void WhitelistedAmountNotCited_ExcludesSiblingWithNowKnownValue()
    {
        Assert.DoesNotContain(WingEnchantMaterialWhitelist.SiblingWithSharedEnchantValueItemId,
            (IEnumerable<int>)WingEnchantMaterialWhitelist.WhitelistedAmountNotCited);
    }

    [Fact]
    public void WhitelistedAmountNotCited_ExcludesFullySpecifiedMaterials()
    {
        Assert.DoesNotContain(WingEnchantMaterialWhitelist.GuaranteedSuccessScrollItemId,
            (IEnumerable<int>)WingEnchantMaterialWhitelist.WhitelistedAmountNotCited);
        Assert.DoesNotContain(WingEnchantMaterialWhitelist.ProtectedMaterialItemId,
            (IEnumerable<int>)WingEnchantMaterialWhitelist.WhitelistedAmountNotCited);
    }

    [Fact]
    public void GuaranteedSuccessScroll_HasExpectedIdAndAmount()
    {
        Assert.Equal(826, WingEnchantMaterialWhitelist.GuaranteedSuccessScrollItemId);
        Assert.Equal(50, WingEnchantMaterialWhitelist.GuaranteedSuccessScrollAmount);
    }

    [Fact]
    public void ProtectedMaterial_HasExpectedIdAndDistinctFailureResultCode()
    {
        Assert.Equal(8106, WingEnchantMaterialWhitelist.ProtectedMaterialItemId);
        Assert.Equal(9, WingEnchantMaterialWhitelist.ProtectedMaterialFailureResultCode);

        Assert.NotEqual(8, WingEnchantMaterialWhitelist.ProtectedMaterialFailureResultCode);
    }

    [Fact]
    public void ProtectedMaterialEnchantValue_IsOne_SharedWithSibling695()
    {
        Assert.Equal(1, WingEnchantMaterialWhitelist.ProtectedMaterialEnchantValue);
        Assert.Equal(695, WingEnchantMaterialWhitelist.SiblingWithSharedEnchantValueItemId);
        Assert.Contains(WingEnchantMaterialWhitelist.SiblingWithSharedEnchantValueItemId,
            (IEnumerable<int>)WingEnchantMaterialWhitelist.ClassWhitelist);
    }

    [Fact]
    public void WingEnchantCpCost_IsFiftyFlatContributionPoints()
    {
        Assert.Equal(50, WingEnchantMaterialWhitelist.WingEnchantCpCost);
    }
}
