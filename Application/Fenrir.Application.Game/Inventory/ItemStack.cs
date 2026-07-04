using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Inventory;

/// <summary>One occupied slot's item state in memory (Container/Slot are the dictionary keys in InventoryState, not repeated here).</summary>
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
}
