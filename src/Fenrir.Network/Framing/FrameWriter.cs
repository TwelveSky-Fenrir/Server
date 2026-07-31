using System.Buffers;
using Fenrir.Network.Compression;

namespace Fenrir.Network.Framing;

public static class FrameWriter
{
    public static int FrameSizeOf<TPacket>()
        where TPacket : struct, IOutgoingPacket
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

    public static byte[] WriteCompressedFrame<TPacket>(in TPacket packet)
        where TPacket : struct, IOutgoingPacket
    {
        var payloadSize = TPacket.PayloadSize;
        var rented = ArrayPool<byte>.Shared.Rent(payloadSize);
        try
        {
            var plainSpan = rented.AsSpan(0, payloadSize);
            packet.Write(plainSpan);
            return Lz4Envelope.Encode(TPacket.Opcode, plainSpan);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }
}
