using Fenrir.Contracts.Packets.Shared;
using Fenrir.Data.Commerce;

namespace Fenrir.Application.Game.Handlers.Commerce;

/// <summary>
///     Shared PROXY_SHOP_USER_INFO builder for <see cref="GetProxyShopHandler" />/
///     <see cref="UpdateProxyShopHandler" /> -- maps offline-shop rows onto the 25-slot wire shape.
/// </summary>
internal static class ProxyShopWireMapper
{
    public const int MaxSlots = 25;

    public static ProxyShopUserInfo Build(string avatarName, OfflineShopRowDto? shop,
        IReadOnlyList<OfflineShopItemRowDto> items)
    {
        var wireItems = new ProxyShopItem[MaxSlots];
        var sockets = new int[MaxSlots * 3];

        foreach (var item in items)
        {
            if (item.SlotIndex is < 0 or >= MaxSlots || item.ItemId is not { } itemId)
                continue;

            wireItems[item.SlotIndex] = new ProxyShopItem
            {
                Id = itemId, Quantity = item.Quantity, Value = item.Value, Serial = item.SerialNumber,
                Price = item.Price
            };
        }

        return new ProxyShopUserInfo
        {
            AvatarName = avatarName,
            Items = wireItems,
            Sockets = sockets,
            Money = shop?.Money ?? 0,
            BigMoney = shop?.BigMoney ?? 0
        };
    }
}
