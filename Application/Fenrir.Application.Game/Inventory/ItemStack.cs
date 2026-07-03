using Fenrir.Data.Characters;

namespace Fenrir.Application.Game.Inventory;

/// <summary>
///     One occupied slot's item state in memory -- the runtime twin of <see cref="CharacterItemSlotDto" />/
///     <see cref="CharacterItemSlotTvp" /> minus their own Container/Slot columns (those are the DICTIONARY
///     KEYS in <see cref="InventoryState" />, not repeated here). Same 4 "upgrade byte" fields
///     <see cref="Fenrir.Application.Game.Stats.EquippedItemSlot" /> already documents (Enchant=IS,
///     Combine=IU, Refine=IM, Socket=IZ, report 11 §2) -- this type is the container-agnostic slot payload
///     shared by every container kind (inventory pages, equipment, store pages), not just equipped items.
/// </summary>
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
    /// <summary>Projects one row of the A3 world-entry item result set (RS1 of usp_Character_GetForWorldEntry) into its in-memory shape.</summary>
    public static ItemStack FromRow(CharacterItemSlotDto row)
    {
        return new ItemStack(row.ItemId, row.Quantity, row.Enchant, row.Combine, row.Refine, row.Socket,
            row.SocketGem1, row.SocketGem2, row.SocketGem3, row.ExpireDate, row.Serial);
    }

    /// <summary>Re-attaches a container-relative slot index for <see cref="CharacterRepository.ReplaceContainerAsync" />'s TVP (D7 regime (b) synchronous flush).</summary>
    public CharacterItemSlotTvp ToTvp(byte slot)
    {
        return new CharacterItemSlotTvp(slot, ItemId, Quantity, Enchant, Combine, Refine, Socket, SocketGem1,
            SocketGem2, SocketGem3, ExpireDate, Serial);
    }

    /// <summary>Re-attaches Container/Slot to rebuild a full row (world-entry seeding / tests) -- inverse of <see cref="FromRow" />.</summary>
    public CharacterItemSlotDto ToRow(byte container, byte slot)
    {
        return new CharacterItemSlotDto(container, slot, ItemId, Quantity, Enchant, Combine, Refine, Socket,
            SocketGem1, SocketGem2, SocketGem3, ExpireDate, Serial);
    }
}
