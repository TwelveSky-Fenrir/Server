using Fenrir.Application.Game.Domain.Commerce;

namespace Fenrir.Application.Game.Tests.Commerce;

/// <summary>
///     C21a -- boundary coverage for <see cref="CostumeSearchWhitelist" />'s hardcoded id ranges. Every
///     assertion here is either a first/last id of a listed range (must be included) or the immediate
///     out-of-range neighbor (must be excluded), matching the contract's explicit "gaps are deliberate
///     exclusions, not omissions" instruction.
/// </summary>
public class CostumeSearchWhitelistTests
{
    [Theory]
    [InlineData(301)]
    [InlineData(402)]
    [InlineData(350)]
    [InlineData(2146)]
    [InlineData(2148)]
    [InlineData(1801)]
    [InlineData(1803)]
    [InlineData(1891)]
    [InlineData(1893)]
    [InlineData(17701)]
    [InlineData(17703)]
    [InlineData(18124)]
    [InlineData(18132)]
    [InlineData(93301)]
    [InlineData(93316)] // re-added after previously being removed, per the contract.
    [InlineData(93317)] // re-added after previously being removed, per the contract.
    [InlineData(93330)]
    [InlineData(93334)]
    [InlineData(93345)]
    [InlineData(93376)]
    [InlineData(93381)]
    [InlineData(93385)]
    [InlineData(93405)]
    [InlineData(76524)]
    [InlineData(76526)]
    public void ListedId_IsWhitelisted(int itemId)
    {
        Assert.True(CostumeSearchWhitelist.Contains(itemId));
    }

    [Theory]
    [InlineData(300)] // one below the 301-402 block.
    [InlineData(403)] // one above the 301-402 block.
    [InlineData(2145)]
    [InlineData(2149)]
    [InlineData(1800)]
    [InlineData(1804)]
    [InlineData(1890)]
    [InlineData(1894)]
    [InlineData(17700)]
    [InlineData(17704)]
    [InlineData(18123)]
    [InlineData(18133)]
    [InlineData(93300)]
    [InlineData(93331)] // still excluded per the contract, even though it's adjacent to 93330.
    [InlineData(93332)] // still excluded per the contract, even though it's adjacent to 93330.
    [InlineData(93333)] // gap between the 93301-93330 and 93334-93345 ranges.
    [InlineData(93346)]
    [InlineData(93375)]
    [InlineData(93382)] // gap between the 93376-93381 and 93385-93405 ranges.
    [InlineData(93383)]
    [InlineData(93384)]
    [InlineData(93406)]
    [InlineData(76523)]
    [InlineData(76527)]
    [InlineData(0)]
    [InlineData(-1)]
    public void UnlistedId_IsNotWhitelisted(int itemId)
    {
        Assert.False(CostumeSearchWhitelist.Contains(itemId));
    }

    [Fact]
    public void CostumeCategory_MatchesExistingRawSort6And9Category()
    {
        // SearchShopListingsService.SortToCategory already maps raw sorts 6/9 (EPSORT_COSTUM) to category 4
        // -- the whitelist's own category constant must resolve to the SAME value so a whitelisted item is
        // filtered exactly like a directly-sorted costume item.
        Assert.Equal(4, CostumeSearchWhitelist.CostumeCategory);
    }

    [Fact]
    public void TotalCount_MatchesContractEnumeration()
    {
        // 102 + 3 + 3 + 3 + 3 + 9 + 30 + 12 + 6 + 21 + 3 = 195, per the contract's own explicit range list.
        Assert.Equal(195, CostumeSearchWhitelist.ItemIds.Count);
    }
}
