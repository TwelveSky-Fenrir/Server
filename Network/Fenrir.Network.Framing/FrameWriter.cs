using Fenrir.Network.Abstractions;
using Fenrir.Network.Compression;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Framing;

// 1-byte opcode header + payload, whole-frame obfuscation (§3.1) if declared. Field-level XOR is already
// baked into the generated Write. Compressed packets bypass this entirely via SendRaw.
public static class FrameWriter
{
    public static int FrameSizeOf<TPacket>() where TPacket : struct, IOutgoingPacket
    {
        return WireHeaderSizes.DefaultPacketSize + TPacket.PayloadSize;
    }

    public static int WriteFrame<TPacket>(in TPacket packet, Span<byte> destination)
        where TPacket : struct, IOutgoingPacket
    {
        var total = FrameSizeOf<TPacket>();
        destination[0] = TPacket.Opcode;
        packet.Write(destination.Slice(WireHeaderSizes.DefaultPacketSize, TPacket.PayloadSize));

        if (TPacket.Obfuscation == WireObfuscationMode.XorPacketGlobal)
            WireXor.ApplyPacketXor(destination[..total]);

        return total;
    }
}
