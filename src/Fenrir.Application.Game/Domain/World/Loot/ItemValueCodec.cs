namespace Fenrir.Application.Game.Domain.World.Loot;

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
