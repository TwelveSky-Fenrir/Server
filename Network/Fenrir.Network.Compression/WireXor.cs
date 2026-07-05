namespace Fenrir.Network.Compression;

/// <summary>
///     Byte-exact legacy XOR primitives (§3.1-3.3); the historical <c>GetMyXor</c> bug degenerates the key into a
///     CONSTANT keystream {0x10, 0xFE, 0xFE, ...}, not an incrementing one.
/// </summary>
public static class WireXor
{
    private const byte FirstKey = 0x10;
    private const byte SteadyKey = 0xFE;

    /// <summary><c>XOR_PACKET(ptr, size)</c>: buf[0]^=0x10; buf[1..size-2]^=0xFE; buf[size-1] untouched.</summary>
    public static void ApplyPacketXor(Span<byte> buffer)
    {
        if (buffer.Length <= 1)
            return;

        buffer[0] ^= FirstKey;
        for (var i = 1; i < buffer.Length - 1; i++)
            buffer[i] ^= SteadyKey;
    }

    /// <summary>
    ///     <c>USE_XOR_UID</c>: XORs only the C-string prefix (up to first null); fragile if length differs between
    ///     send/receive (§3.3) — legacy behavior reproduced as-is.
    /// </summary>
    public static void ApplyUidXor(Span<byte> fixedField)
    {
        var length = fixedField.IndexOf((byte)0);
        if (length < 0)
            length = fixedField.Length;

        ApplyPacketXor(fixedField[..length]);
    }

    /// <summary><c>scopyAvtXorInt</c>: 4-byte int, all 4 bytes XORed (none spared).</summary>
    public static void XorInt(Span<byte> four)
    {
        four[0] ^= FirstKey;
        for (var i = 1; i < four.Length; i++)
            four[i] ^= SteadyKey;
    }

    /// <summary><c>scopyAvtXorIntArr</c>: byte array, last 2 bytes stay in clear.</summary>
    public static void XorIntArray(Span<byte> buffer)
    {
        if (buffer.Length == 0)
            return;

        buffer[0] ^= FirstKey;
        for (var i = 1; i < buffer.Length - 2; i++)
            buffer[i] ^= SteadyKey;
    }

    /// <summary><c>scopyAvtXorChar</c>: same bounds as <see cref="XorIntArray" />, then forces the last byte to 0.</summary>
    public static void XorChar(Span<byte> buffer)
    {
        if (buffer.Length == 0)
            return;

        XorIntArray(buffer);
        buffer[^1] = 0;
    }

    /// <summary><c>scopyAvtXorChar2</c>: fixed-width string matrix, <see cref="XorChar" /> applied row by row.</summary>
    public static void XorChar2Rows(Span<byte> buffer, int rowLength)
    {
        for (var offset = 0; offset < buffer.Length; offset += rowLength)
            XorChar(buffer.Slice(offset, rowLength));
    }

    /// <summary>
    ///     <c>mPacketEncryptionValue</c> (§3.4): uniform single-byte XOR, distinct from <see cref="ApplyPacketXor" />'s
    ///     degenerate keystream; key == 0 means unseeded (plaintext), matching legacy behavior.
    /// </summary>
    public static void ApplyStreamXor(Span<byte> buffer, byte key)
    {
        if (key == 0)
            return;

        for (var i = 0; i < buffer.Length; i++)
            buffer[i] ^= key;
    }
}
