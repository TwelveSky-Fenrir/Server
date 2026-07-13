namespace Fenrir.Core.Wire;

/// <summary>
/// XOR legacy (obfuscation, <b>pas</b> de la cryptographie) : premier octet ^ <c>0x10</c>, octets suivants
/// ^ <c>0xFE</c> (saturant), dernier octet non touché ; involutif. Sert au XOR de paquet entier
/// (<c>XorPacketGlobal</c>), au XOR de champ avatar, à l'UID obfusqué et au XOR de flux au handshake.
/// </summary>
public static class WireXor
{
    private const byte FirstKey = 0x10;
    private const byte SteadyKey = 0xFE;

    public static void ApplyPacketXor(Span<byte> buffer)
    {
        if (buffer.Length <= 1)
            return;

        buffer[0] ^= FirstKey;
        for (var i = 1; i < buffer.Length - 1; i++)
            buffer[i] ^= SteadyKey;
    }

    public static void ApplyUidXor(Span<byte> fixedField)
    {
        var length = fixedField.IndexOf((byte)0);
        if (length < 0)
            length = fixedField.Length;

        ApplyPacketXor(fixedField[..length]);
    }

    public static void XorInt(Span<byte> four)
    {
        four[0] ^= FirstKey;
        for (var i = 1; i < four.Length; i++)
            four[i] ^= SteadyKey;
    }

    public static void XorIntArray(Span<byte> buffer)
    {
        if (buffer.Length == 0)
            return;

        buffer[0] ^= FirstKey;
        for (var i = 1; i < buffer.Length - 2; i++)
            buffer[i] ^= SteadyKey;
    }

    public static void XorChar(Span<byte> buffer)
    {
        if (buffer.Length == 0)
            return;

        XorIntArray(buffer);
        buffer[^1] = 0;
    }

    public static void XorChar2Rows(Span<byte> buffer, int rowLength)
    {
        for (var offset = 0; offset < buffer.Length; offset += rowLength)
            XorChar(buffer.Slice(offset, rowLength));
    }

    public static void ApplyStreamXor(Span<byte> buffer, byte key)
    {
        if (key == 0)
            return;

        for (var i = 0; i < buffer.Length; i++)
            buffer[i] ^= key;
    }
}
