using Fenrir.Application.Game.Domain.Enchant;

namespace Fenrir.Application.Game.Tests.Enchant;

/// <summary>
///     Covers <see cref="WingEnchantMaterialWhitelist" /> (workstream C12-warlord-chest, "part C") -- the
///     Gate-1 class whitelist, the confirmed Gate-1/Gate-2 mismatch (2387/2392), the dead-in-production
///     99409 exclusion, and the two fully-specified materials (826 guaranteed-success, 8106 protected).
/// </summary>
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
        Assert.True(WingEnchantMaterialWhitelist.ClassWhitelist.Contains(itemId));
    }

    [Fact]
    public void ClassWhitelist_ExcludesDeadOnlineForDsId()
    {
        // 99409 is textually present in source but ONLINE_FOR_DS-gated, dead in every real build.
        Assert.False(WingEnchantMaterialWhitelist.ClassWhitelist.Contains(99409));
    }

    [Theory]
    [InlineData(2387)]
    [InlineData(2392)]
    public void Gate2MissingDisconnects_ContainsOnlyTheTwoConfirmedInconsistentIds(int itemId)
    {
        Assert.True(WingEnchantMaterialWhitelist.Gate2MissingDisconnects.Contains(itemId));
        Assert.True(WingEnchantMaterialWhitelist.ClassWhitelist.Contains(itemId));
    }

    [Fact]
    public void Gate2MissingDisconnects_HasExactlyTwoEntries()
    {
        Assert.Equal(2, WingEnchantMaterialWhitelist.Gate2MissingDisconnects.Count);
    }

    [Theory]
    [InlineData(695)]
    [InlineData(696)]
    [InlineData(698)]
    [InlineData(2397)]
    public void WhitelistedAmountNotCited_ContainsTheFourUnamountedIds(int itemId)
    {
        Assert.True(WingEnchantMaterialWhitelist.WhitelistedAmountNotCited.Contains(itemId));
    }

    [Fact]
    public void WhitelistedAmountNotCited_ExcludesFullySpecifiedMaterials()
    {
        Assert.False(WingEnchantMaterialWhitelist.WhitelistedAmountNotCited.Contains(
            WingEnchantMaterialWhitelist.GuaranteedSuccessScrollItemId));
        Assert.False(WingEnchantMaterialWhitelist.WhitelistedAmountNotCited.Contains(
            WingEnchantMaterialWhitelist.ProtectedMaterialItemId));
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

        // Distinct from the non-wing 8101 material's own failure result code (8).
        Assert.NotEqual(8, WingEnchantMaterialWhitelist.ProtectedMaterialFailureResultCode);
    }
}
