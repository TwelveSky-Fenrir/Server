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
        Assert.Contains(itemId, WingEnchantMaterialWhitelist.ClassWhitelist);
    }

    [Fact]
    public void ClassWhitelist_ExcludesDeadOnlineForDsId()
    {
        // 99409 is textually present in source but ONLINE_FOR_DS-gated, dead in every real build.
        Assert.DoesNotContain(99409, WingEnchantMaterialWhitelist.ClassWhitelist);
    }

    [Theory]
    [InlineData(2387)]
    [InlineData(2392)]
    public void Gate2MissingDisconnects_ContainsOnlyTheTwoConfirmedInconsistentIds(int itemId)
    {
        Assert.Contains(itemId, WingEnchantMaterialWhitelist.Gate2MissingDisconnects);
        Assert.Contains(itemId, WingEnchantMaterialWhitelist.ClassWhitelist);
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
        Assert.Contains(itemId, WingEnchantMaterialWhitelist.WhitelistedAmountNotCited);
    }

    /// <summary>
    ///     695's enchant-VALUE was recovered by the 2026-07-11 supplemental finding (shares 8106's flat +1),
    ///     so it is no longer an "amount not cited" id -- only its failure-path behavior remains open.
    /// </summary>
    [Fact]
    public void WhitelistedAmountNotCited_ExcludesSiblingWithNowKnownValue()
    {
        Assert.DoesNotContain(WingEnchantMaterialWhitelist.SiblingWithSharedEnchantValueItemId,
            WingEnchantMaterialWhitelist.WhitelistedAmountNotCited);
    }

    [Fact]
    public void WhitelistedAmountNotCited_ExcludesFullySpecifiedMaterials()
    {
        Assert.DoesNotContain(WingEnchantMaterialWhitelist.GuaranteedSuccessScrollItemId,
            WingEnchantMaterialWhitelist.WhitelistedAmountNotCited);
        Assert.DoesNotContain(WingEnchantMaterialWhitelist.ProtectedMaterialItemId,
            WingEnchantMaterialWhitelist.WhitelistedAmountNotCited);
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

    /// <summary>2026-07-11 supplemental finding: 8106 and its sibling 695 share the same flat +1 enchant value.</summary>
    [Fact]
    public void ProtectedMaterialEnchantValue_IsOne_SharedWithSibling695()
    {
        Assert.Equal(1, WingEnchantMaterialWhitelist.ProtectedMaterialEnchantValue);
        Assert.Equal(695, WingEnchantMaterialWhitelist.SiblingWithSharedEnchantValueItemId);
        Assert.Contains(WingEnchantMaterialWhitelist.SiblingWithSharedEnchantValueItemId,
            WingEnchantMaterialWhitelist.ClassWhitelist);
    }

    /// <summary>
    ///     2026-07-11 supplemental finding: the Wing enchant cost is a flat 50 CP charged by the equipped
    ///     item's category, not by material -- distinct from the material-priced money cost the non-wing
    ///     path uses.
    /// </summary>
    [Fact]
    public void WingEnchantCpCost_IsFiftyFlatContributionPoints()
    {
        Assert.Equal(50, WingEnchantMaterialWhitelist.WingEnchantCpCost);
    }
}
