using System.Buffers;
using Fenrir.Network.Abstractions;

namespace Fenrir.Network.Framing;

public static class S2SFrameReader
{
    public static bool TryReadFrame(ref ReadOnlySequence<byte> buffer, IOpcodeFrameSizeProvider registry,
        FenrirServer server, out Frame frame)
    {
        frame = default;

        if (buffer.Length < WireHeaderSizes.DefaultPacketSize)
            return false;

        Span<byte> header = stackalloc byte[WireHeaderSizes.DefaultPacketSize];
        buffer.Slice(0, WireHeaderSizes.DefaultPacketSize).CopyTo(header);
        var opcode = header[0];

        if (!registry.TryGetFrameSize(opcode, out var frameSize))
            throw new ProtocolViolationException(server, opcode);

        if (buffer.Length < frameSize)
            return false;

        var payload = buffer.Slice(WireHeaderSizes.DefaultPacketSize, frameSize - WireHeaderSizes.DefaultPacketSize);
        frame = new Frame(server, opcode, payload);
        buffer = buffer.Slice(frameSize);
        return true;
    }
}
