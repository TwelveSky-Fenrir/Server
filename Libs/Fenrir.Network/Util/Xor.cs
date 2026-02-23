namespace Fenrir.Network.Util;

public static partial class Utils
{
    public static void Xor(Span<byte> data, byte key)
    {
        if (key == 0x00) return;
        for (int i = 0; i < data.Length; i++)
        {
            data[i] ^= key;
        }
    }
}