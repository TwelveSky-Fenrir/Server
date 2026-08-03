using System.Buffers;

namespace Fenrir.Network.Framing;

public static class FrameReader
{
    public static bool TryReadFrame(ref ReadOnlySequence<byte> buffer, IOpcodeFrameSizeProvider registry,
        FenrirServer server, out Frame frame)
    {
        frame = default;

        if (buffer.Length < WireHeaderSizes.ClientPacketSize)
            return false;

        Span<byte> header = stackalloc byte[WireHeaderSizes.ClientPacketSize];
        buffer.Slice(0, WireHeaderSizes.ClientPacketSize).CopyTo(header);
        var opcode = header[8];

        if (!registry.TryGetFrameSize(opcode, out var frameSize))
            throw new ProtocolViolationException(server, opcode);

        if (frameSize < WireHeaderSizes.ClientPacketSize)
            throw new ProtocolViolationException(server, opcode);

        if (buffer.Length < frameSize)
            return false;

        var payload = buffer.Slice(WireHeaderSizes.ClientPacketSize, frameSize - WireHeaderSizes.ClientPacketSize);
        frame = new Frame(server, opcode, payload);
        buffer = buffer.Slice(frameSize);
        return true;
    }
}
