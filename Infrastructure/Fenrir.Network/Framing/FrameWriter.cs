using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Wire;

namespace Fenrir.Network.Framing;

/// <summary>
///     Lays out one outgoing legacy frame (<c>DEFALUT_PACKET</c>: 1-byte opcode header + payload) and applies
///     whole-frame obfuscation (§3.1) where the packet declares it. Field-level obfuscation
///     (<see cref="LegacyObfuscation.XorFieldAvatar" />) and the <c>tID</c> double-XOR
///     (<c>[LegacyUidField]</c>) are already baked into the generated <c>Write</c> itself — nothing extra to do
///     here for those. LZ4-compressed packets (<see cref="IOutgoingPacket" /> types with <c>Compressed = true</c>
///     on their <c>[FenrirPacket]</c>) bypass this entirely: callers send the pre-built ZPACKET-enveloped frame
///     from the generated <c>{Login|Zone}MessageFactory.Encode</c> via <see cref="Sessions.ClientSession.SendRaw" />.
/// </summary>
public static class FrameWriter
{
    public static int FrameSizeOf<TPacket>() where TPacket : struct, IOutgoingPacket
    {
        return LegacyHeaders.DefaultPacketSize + TPacket.PayloadSize;
    }

    public static int WriteFrame<TPacket>(in TPacket packet, Span<byte> destination)
        where TPacket : struct, IOutgoingPacket
    {
        var total = FrameSizeOf<TPacket>();
        destination[0] = TPacket.Opcode;
        packet.Write(destination.Slice(LegacyHeaders.DefaultPacketSize, TPacket.PayloadSize));

        if (TPacket.Obfuscation == LegacyObfuscation.XorPacketGlobal)
            LegacyXor.ApplyPacketXor(destination[..total]);

        return total;
    }
}
