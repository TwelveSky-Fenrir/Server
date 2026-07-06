using Fenrir.Application.Game.Domain.Commerce;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.World;

/// <summary>
///     <see cref="Zone.TryUpdateProxyShopExpiration" /> -- the best-effort live-registry mirror used by
///     <c>UseInventoryItemService</c>'s proxy-shop-rental-extension branch.
/// </summary>
public class ZoneProxyShopsTests
{
    private static ProxyShopBroadcastEntry Entry(int characterId, int shopDate)
    {
        return new ProxyShopBroadcastEntry(characterId, characterId * 2 + 1, "Owner", "Shop", 0f, 0f, 0f, shopDate);
    }

    [Fact]
    public void TryUpdateProxyShopExpiration_NoRegisteredEntry_ReturnsFalse_AndDoesNothing()
    {
        var zone = ZoneTestKit.CreateZone(1);

        var updated = zone.TryUpdateProxyShopExpiration(10, 20260714);

        Assert.False(updated);
        Assert.Equal(0, zone.ProxyShopCount);
    }

    [Fact]
    public void TryUpdateProxyShopExpiration_RegisteredEntry_UpdatesShopDateInPlace_AndReturnsTrue()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var entry = Entry(10, 20260707);
        zone.RegisterProxyShop(entry);

        var updated = zone.TryUpdateProxyShopExpiration(10, 20260714);

        Assert.True(updated);
        Assert.Equal(1, zone.ProxyShopCount);
        // Registration keeps this exact instance -- reading it back directly is a stronger assertion than
        // ProxyShopCount alone, since Zone exposes no public getter for an individual entry.
        Assert.Equal(20260714, entry.ShopDate);
    }

    [Fact]
    public void TryUpdateProxyShopExpiration_DifferentCharacter_LeavesOtherEntriesUntouched()
    {
        var zone = ZoneTestKit.CreateZone(1);
        var entry = Entry(10, 20260707);
        zone.RegisterProxyShop(entry);

        var updated = zone.TryUpdateProxyShopExpiration(999, 20260714);

        Assert.False(updated);
        Assert.Equal(1, zone.ProxyShopCount);
        Assert.Equal(20260707, entry.ShopDate);
    }
}
