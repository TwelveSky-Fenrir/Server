using System.Buffers;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Compression;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Framing;

// 1-byte opcode header + payload, whole-frame obfuscation if declared. Field-level XOR is already
// baked into the generated Write. Compressed packets go through WriteCompressedFrame instead (a
// different envelope shape entirely -- see that method's own remarks), never through WriteFrame.
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

    /// <summary>
    ///     ZC_ZPACKET_OK_SEND envelope (§3.5): tProtocol(1) + isCompress(4) + originalSize(4) + compressSize(4) +
    ///     payload -- a different header shape than <see cref="WriteFrame{TPacket}" />'s plain 1-byte prefix, so
    ///     this is a genuinely separate encode path, not a variant of it. Generic over every
    ///     <c>TPacket.Compressed == true</c> packet rather than one generated method per packet type: the whole
    ///     body only ever touches <c>TPacket.PayloadSize</c>/<c>.Write</c>/<c>.Opcode</c>, so there is nothing
    ///     genuinely per-packet-type to generate.
    /// </summary>
    public static byte[] WriteCompressedFrame<TPacket>(in TPacket packet) where TPacket : struct, IOutgoingPacket
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
