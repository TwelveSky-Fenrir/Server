namespace Fenrir.Application.Game.World.Loot;

/// <summary>
///     Ports the legacy "packed item value" int (<c>SetISIUIMValue</c>/<c>SetIMValue</c>/<c>SetIZValue</c>,
///     <c>Server/Header/function.h:385-436</c>, verified against source) to/from the 4 separate upgrade bytes
///     Fenrir already stores as distinct columns (<c>ItemStack.Enchant/Combine/Refine/Socket</c>, report 11
///     §2). The legacy packs all 4 into ONE <c>int iValue</c> by writing each into its own byte of a 4-byte
///     buffer via <c>CopyMemory</c> (little-endian x86, byte0=lowest): byte0 = IS/Enchant, byte1 = IU/Combine,
///     byte2 = IM/Refine, byte3 = IZ/Socket. <see cref="ObjectForItem.Value" />/<see cref="ObjectForMonster" />
///     drop replication and <c>ProcessForGetItem</c>'s <c>SetInventory(..., iValue, ...)</c> call both move
///     this SAME packed int between the ground-item wire shape and the inventory row -- this codec is the one
///     place Fenrir needs to cross that boundary (ground items carry the packed int; <c>ItemStack</c> carries
///     the 4 unpacked bytes).
/// </summary>
public static class ItemValueCodec
{
    public static int Encode(byte enchant, byte combine, byte refine, byte socket)
    {
        return enchant | (combine << 8) | (refine << 16) | (socket << 24);
    }

    public static (byte Enchant, byte Combine, byte Refine, byte Socket) Decode(int value)
    {
        return (
            (byte)(value & 0xFF),
            (byte)((value >> 8) & 0xFF),
            (byte)((value >> 16) & 0xFF),
            (byte)((value >> 24) & 0xFF));
    }
}
