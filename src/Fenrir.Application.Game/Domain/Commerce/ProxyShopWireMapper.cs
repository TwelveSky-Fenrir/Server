using Fenrir.Core.Packets.Shared;

namespace Fenrir.Application.Game.Domain.Commerce;

public static class ProxyShopWireMapper
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
