namespace Fenrir.Framework.Helpers;

public static class XorHelper
{
    // TODO: Why is there two implementations of Xor?
    private const byte XorKey = 0x00;

    public static byte[] ApplyXor(byte[] data, int length)
    {
        var result = new byte[length];
        for (var i = 0; i < length; i++) result[i] = (byte)(data[i] ^ XorKey);
        return result;
    }

    public static byte[] ApplyXor(ReadOnlyMemory<byte> responseMetadataMessagePayload, int byteSize)
    {
        var dataBuffer = responseMetadataMessagePayload.ToArray();

        return ApplyXor(dataBuffer, byteSize);
    }
}