namespace Fenrir.Application.Game.Domain.World.Loot;

public static class ItemStateEncoder
{
    public static int ChangeEnchant(int packed, int delta)
    {
        return SetByte(packed, 0, GetByte(packed, 0) + delta);
    }

    public static int ChangeCombine(int packed, int delta)
    {
        return SetByte(packed, 1, GetByte(packed, 1) + delta);
    }

    public static int ChangeRefine(int packed, int delta)
    {
        return SetByte(packed, 2, GetByte(packed, 2) + delta);
    }

    public static int ResetEnchant(int packed)
    {
        return SetByte(packed, 0, 0);
    }

    public static int ResetCombine(int packed)
    {
        return SetByte(packed, 1, 0);
    }

    public static int ResetRefine(int packed)
    {
        return SetByte(packed, 2, 0);
    }

    public static int SetRefine(int packed, int value)
    {
        return SetByte(packed, 2, value);
    }

    public static int SetSocket(int packed, int value)
    {
        return SetByte(packed, 3, value);
    }

    public static int ResetSocket(int packed)
    {
        return SetByte(packed, 3, 0);
    }

    public static int SetAll(int enchant, int combine, int refine, int socket = 0)
    {
        return ItemValueCodec.Encode((byte)enchant, (byte)combine, (byte)refine, (byte)socket);
    }

    private static sbyte GetByte(int packed, int position)
    {
        return (sbyte)(packed >> (position * 8));
    }

    private static int SetByte(int packed, int position, int rawValue)
    {
        var shift = position * 8;
        var clearMask = ~(0xFF << shift);
        return (packed & clearMask) | ((rawValue & 0xFF) << shift);
    }
}
