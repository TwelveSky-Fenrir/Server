using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Domain.Inventory;

public readonly record struct ItemStack(
    int ItemId,
    int Quantity,
    byte Enchant,
    byte Combine,
    byte Refine,
    byte Socket,
    int SocketGem1,
    int SocketGem2,
    int SocketGem3,
    int ExpireDate,
    int Serial)
{
    public static ItemStack FromRow(CharacterItemSlotDto row)
    {
        return new ItemStack(row.ItemId, row.Quantity, row.Enchant, row.Combine, row.Refine, row.Socket,
            row.SocketGem1, row.SocketGem2, row.SocketGem3, row.ExpireDate, row.Serial);
    }

    public CharacterItemSlotTvp ToTvp(byte slot)
    {
        return new CharacterItemSlotTvp(slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1,
            SocketGem2, SocketGem3, ExpireDate, Serial);
    }

    public CharacterItemSlotDto ToRow(byte container, byte slot)
    {
        return new CharacterItemSlotDto(container, slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
            SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial);
    }

    // V1 perd gemmes et expiration, que le legacy porte pourtant (Server/ts25zone/S04_MyWork05.cpp:2999-3000
    // depot, :3104-3105 retrait) ; gardee vivante tant que game.usp_AccountVault_* n'a pas bascule en V2.
    public AccountVaultItemSlotTvp ToVaultTvp(short slotIndex)
    {
        return new AccountVaultItemSlotTvp(slotIndex, ItemId, Quantity,
            ItemValueCodec.Encode(Enchant, Combine, Refine, Socket), Serial, null);
    }

    public static ItemStack FromVaultRow(AccountVaultItemSlotDto row)
    {
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(row.Value);
        return new ItemStack(row.ItemId ?? 0, row.Quantity, enchant, combine, refine, socket, 0, 0, 0, 0,
            row.SerialNumber);
    }

    public AccountVaultItemSlotV2Tvp ToVaultTvpV2(short slotIndex)
    {
        return new AccountVaultItemSlotV2Tvp(slotIndex, ItemId, Quantity,
            ItemValueCodec.Encode(Enchant, Combine, Refine, Socket), Serial, null, SocketGem1, SocketGem2,
            SocketGem3, ExpireDate);
    }

    public static ItemStack FromVaultRowV2(AccountVaultItemSlotV2Dto row)
    {
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(row.Value);
        return new ItemStack(row.ItemId ?? 0, row.Quantity, enchant, combine, refine, socket, row.SocketGem1,
            row.SocketGem2, row.SocketGem3, row.ExpireDate, row.SerialNumber);
    }
}
