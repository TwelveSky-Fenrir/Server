using System.Buffers;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Framing;

// No length prefix on the wire; frame size comes from the injected registry, like the legacy
// W_FUNCTION[opcode].SIZE table (§2.3). registry is injected (rather than a hardcoded OpcodeRegistry call)
// so this shared project compiles with zero reference to either split Serialization project -- each server
// passes its own generated LoginOpcodeRegistry.Provider/ZoneOpcodeRegistry.Provider in.
public static class FrameReader
{
    // 9-byte header, opcode at offset 8; throws ProtocolViolationException for an opcode the legacy client would never send.
    public static bool TryReadFrame(ref ReadOnlySequence<byte> buffer, IOpcodeFrameSizeProvider registry,
        FenrirServer server, out Frame frame)
    {
        frame = default;

        if (buffer.Length < WireHeaderSizes.ClientPacketSize)
            return false;

        Span<byte> header = stackalloc byte[WireHeaderSizes.ClientPacketSize];
        buffer.Slice(0, WireHeaderSizes.ClientPacketSize).CopyTo(header);
        var opcode = header[8]; // tProtocol; tPacket1/tPacket2 (offsets 0/4) carry no framing information in BuildEU33

        if (!registry.TryGetFrameSize(opcode, out var frameSize))
            throw new ProtocolViolationException(server, opcode);

        if (buffer.Length < frameSize)
            return false; // header seen, payload not fully arrived yet — wait for more bytes

        var payload = buffer.Slice(WireHeaderSizes.ClientPacketSize, frameSize - WireHeaderSizes.ClientPacketSize);
        frame = new Frame(server, opcode, payload);
        buffer = buffer.Slice(frameSize);
        return true;
    }
}
