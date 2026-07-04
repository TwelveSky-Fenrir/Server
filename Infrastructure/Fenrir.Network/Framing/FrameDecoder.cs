using System.Buffers;
using Fenrir.Contracts.Wire;

namespace Fenrir.Network.Framing;

/// <summary>
///     Splits the inbound legacy byte stream into frames, copy-free. There is no length prefix on the wire — the
///     legacy server (and this decoder) trusts <see cref="OpcodeRegistry" /> alone to know each opcode's total
///     frame size, exactly like the original <c>W_FUNCTION[opcode].SIZE</c> table (§2.3 of the wire contract).
/// </summary>
public static class FrameDecoder
{
    /// <summary>
    ///     Attempts to read one complete <c>CLIENT_PACKET</c>-framed message (9-byte header, opcode at offset 8) from
    ///     <paramref name="buffer" />. Advances <paramref name="buffer" /> past the frame on success. Throws
    ///     <see cref="ProtocolViolationException" /> for an opcode the legacy client would never send — the caller
    ///     must treat that as an unconditional, clean disconnect.
    /// </summary>
    public static bool TryReadFrame(ref ReadOnlySequence<byte> buffer, FenrirServer server, out Frame frame)
    {
        frame = default;

        if (buffer.Length < WireHeaderSizes.ClientPacketSize)
            return false;

        Span<byte> header = stackalloc byte[WireHeaderSizes.ClientPacketSize];
        buffer.Slice(0, WireHeaderSizes.ClientPacketSize).CopyTo(header);
        var opcode = header[8]; // tProtocol; tPacket1/tPacket2 (offsets 0/4) carry no framing information in BuildEU33

        int frameSize;
        try
        {
            frameSize = OpcodeRegistry.FrameSizeOf(server, FenrirDirection.Incoming, opcode);
        }
        catch (ArgumentOutOfRangeException)
        {
            throw new ProtocolViolationException(server, opcode);
        }

        if (buffer.Length < frameSize)
            return false; // header seen, payload not fully arrived yet — wait for more bytes

        var payload = buffer.Slice(WireHeaderSizes.ClientPacketSize, frameSize - WireHeaderSizes.ClientPacketSize);
        frame = new Frame(server, opcode, payload);
        buffer = buffer.Slice(frameSize);
        return true;
    }
}
