using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Abstractions;

/// <summary>
///     Server → client packet. <c>destination</c> starts after the <c>DEFALUT_PACKET</c> header (the 1-byte opcode is
///     written by the send layer via <see cref="IFenrirPacket.Opcode" />).
/// </summary>
public interface IOutgoingPacket : IFenrirPacket
{
    /// <summary>
    ///     Whole-frame obfuscation the send layer must apply after writing header+payload (§3.1). Field-level obfuscation
    ///     (<see cref="LegacyObfuscation.XorFieldAvatar" />) is already baked into <see cref="Write" /> itself and needs no
    ///     extra step here.
    /// </summary>
    public static abstract LegacyObfuscation Obfuscation { get; }

    public int Write(Span<byte> destination);
}
