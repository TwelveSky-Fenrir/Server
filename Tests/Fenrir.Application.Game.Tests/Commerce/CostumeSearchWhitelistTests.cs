using Fenrir.Application.Game.Domain.Commerce;

namespace Fenrir.Application.Game.Tests.Commerce;

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
    [InlineData(93316)]
    [InlineData(93317)]
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
    [InlineData(300)]
    [InlineData(403)]
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
    [InlineData(93331)]
    [InlineData(93332)]
    [InlineData(93333)]
    [InlineData(93346)]
    [InlineData(93375)]
    [InlineData(93382)]
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
        Assert.Equal(4, CostumeSearchWhitelist.CostumeCategory);
    }

    [Fact]
    public void TotalCount_MatchesContractEnumeration()
    {
        Assert.Equal(195, CostumeSearchWhitelist.ItemIds.Count);
    }
}
