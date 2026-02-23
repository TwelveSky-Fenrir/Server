namespace Fenrir.Framework.Extensions;

public static class XorEncryption
{
    // Note: Using 00 as the key will basically have no effect.
    private const byte XorKey = 0x00;

    // TODO: Put Key in a per connection context.
    // TODO: Use Span?
    // I wonder if XorKey is array and it cycles through them?
    // Note: From server side, pretty sure we only had Decryption unless something has changed with newer clients.
    public static byte[] Encrypt(byte[] data)
    {
        if (XorKey == 0x00) return data;
        for (var i = 0; i < data.Length; i++) data[i] ^= XorKey;
        return data;
    }

    public static byte[] Encrypt(byte[] data, int offset, int length)
    {
        if (XorKey == 0x00) return data;
        for (var i = offset; i < offset + length; i++) data[i] ^= XorKey;
        return data;
    }

    public static void Encrypt(Span<byte> data)
    {
        if (XorKey == 0x00) return;
        for (var i = 0; i < data.Length; i++) data[i] ^= XorKey;
    }

    public static byte[] Decrypt(byte[] data)
    {
        return Encrypt(data);
    }

    public static void Decrypt(Span<byte> data)
    {
        Encrypt(data);
    }
}