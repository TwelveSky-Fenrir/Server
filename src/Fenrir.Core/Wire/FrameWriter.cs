using Fenrir.Core.Abstractions;

namespace Fenrir.Core.Wire;

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
        // TPacket.Compressed est static abstract : la branche est eliminee a la compilation pour les
        // paquets non compresses. Sans ce garde, un paquet compresse ecrit ici part EN CLAIR la ou le
        // client attend une enveloppe LZ4 -- et le protocole n'ayant pas de length-prefix, la session se
        // desynchronise definitivement au lieu de lever.
        if (TPacket.Compressed)
            throw new InvalidOperationException(
                $"Packet opcode {TPacket.Opcode} declares Compressed = true and cannot be written through " +
                "FrameWriter.WriteFrame, which emits a plain frame into a caller-sized Span. Its encoded " +
                "length is unknown before compression. Send it through IPacketSession.Send, which branches " +
                "to the LZ4 path.");

        var total = FrameSizeOf<TPacket>();
        destination[0] = TPacket.Opcode;
        packet.Write(destination.Slice(WireHeaderSizes.DefaultPacketSize, TPacket.PayloadSize));

        if (TPacket.Obfuscation == WireObfuscationMode.XorPacketGlobal)
            WireXor.ApplyPacketXor(destination[..total]);

        return total;
    }
}
