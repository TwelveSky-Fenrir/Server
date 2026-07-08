namespace Fenrir.Network.Abstractions;

/// <summary><c>destination</c> starts after the header; the opcode byte is written by the send layer.</summary>
public interface IOutgoingPacket : IFenrirPacket
{
    /// <summary>
    ///     Whole-frame obfuscation applied after writing header+payload; field-level XOR is already baked into
    ///     <see cref="Write" />.
    /// </summary>
    public static abstract WireObfuscationMode Obfuscation { get; }

    /// <summary>ZPACKET + LZ4 envelope on send (<c>Fenrir.Network.Compression.Lz4Envelope</c>), instead of the plain frame.</summary>
    public static abstract bool Compressed { get; }

    public int Write(Span<byte> destination);
}
