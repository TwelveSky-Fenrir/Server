namespace Fenrir.Application.Game.World.Loot;

/// <summary>
///     Packs/unpacks the legacy "item value" int (<c>SetISIUIMValue</c>, <c>function.h:385-436</c>) to/from the
///     4 upgrade bytes Fenrir stores as separate columns. Legacy byte order (little-endian): byte0=Enchant,
///     byte1=Combine, byte2=Refine, byte3=Socket.
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
