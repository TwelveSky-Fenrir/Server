using System.Buffers;
using Fenrir.Network.Compression;

namespace Fenrir.Network.Framing;

public static class CompressedFrameWriter
{
    public static byte[] WriteCompressedFrame<TPacket>(in TPacket packet)
        where TPacket : struct, IOutgoingPacket
    {
        if (TPacket.Obfuscation != WireObfuscationMode.None)
            throw new InvalidOperationException(
                $"Packet opcode {TPacket.Opcode} declares Compressed = true together with " +
                $"Obfuscation = {TPacket.Obfuscation}. The LZ4 path applies no packet XOR, so the frame " +
                "would leave unobfuscated. Decide where the XOR applies relative to compression before " +
                "declaring both.");

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
