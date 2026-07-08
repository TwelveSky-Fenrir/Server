using Fenrir.Application.Game.Domain.World.Loot;

namespace Fenrir.Application.Game.Domain.Inventory;

/// <summary>
///     One occupied slot's item state in memory (Container/Slot are the dictionary keys in InventoryState, not
///     repeated here).
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

    /// <summary>
    ///     game.AccountVault vault-slot shape (the account-scoped "Save"/bank). Unlike <see cref="ToTvp" />,
    ///     drops <see cref="SocketGem1" />/<see cref="SocketGem2" />/<see cref="SocketGem3" />/
    ///     <see cref="ExpireDate" /> -- game.AccountVaultItems has no columns for them (no established
    ///     SocketData encoding exists anywhere in this codebase yet, same posture as
    ///     <c>OfflineShopItemRowDto.SocketData</c>; ExpireDate has no column at all). A gem-socketed or rental
    ///     item loses that state on a deposit into the vault -- a known, flagged gap, not a silent loss a
    ///     caller should assume is safe to ignore.
    /// </summary>
    public AccountVaultItemSlotTvp ToVaultTvp(short slotIndex)
    {
        return new AccountVaultItemSlotTvp(slotIndex, ItemId, Quantity,
            ItemValueCodec.Encode(Enchant, Combine, Refine, Socket), Serial, null);
    }

    /// <summary>
    ///     Inverse of <see cref="ToVaultTvp" />; SocketGem1-3/ExpireDate always come back zero -- see that method's own
    ///     remarks.
    /// </summary>
    public static ItemStack FromVaultRow(AccountVaultItemSlotDto row)
    {
        var (enchant, combine, refine, socket) = ItemValueCodec.Decode(row.Value);
        return new ItemStack(row.ItemId ?? 0, row.Quantity, enchant, combine, refine, socket, 0, 0, 0, 0,
            row.SerialNumber);
    }
}
