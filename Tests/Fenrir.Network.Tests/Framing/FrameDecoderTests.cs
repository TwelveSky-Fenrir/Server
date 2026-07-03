using System.Buffers;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Wire;
using Fenrir.Network.Framing;

namespace Fenrir.Network.Tests.Framing;

public class FrameDecoderTests
{
    // HeartbeatRequest/ZoneHandshakeRequest are both Zone-owned [FenrirPacket]s — the server value only
    // matters for opcode-table selection, so a single constant keeps the frame-shaped tests focused.
    private const FenrirServer Server = FenrirServer.Zone;

    // Login Incoming tops out at 25, Zone Incoming at 151 (see Opcodes.cs) — 250 is registered nowhere,
    // for either server, in either direction.
    private const byte UnregisteredOpcode = 250;

    [Fact]
    public void TryReadFrame_SingleCompleteFrame_CzHeartbeatSend_DecodesOpcodeAndPayload()
    {
        var frameBytes = BuildFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize, 1);
        var buffer = new ReadOnlySequence<byte>(frameBytes);

        var decoded = FrameDecoder.TryReadFrame(ref buffer, Server, out var frame);

        Assert.True(decoded);
        Assert.Equal(Server, frame.Server);
        Assert.Equal(HeartbeatRequest.Opcode, frame.Opcode);
        Assert.Equal(frameBytes[LegacyHeaders.ClientPacketSize..], frame.Payload.ToArray());
        Assert.Equal(0, buffer.Length); // the whole frame — and nothing more — was consumed
    }

    [Fact]
    public void TryReadFrame_ThreeFramesBackToBack_EachCallYieldsOneFrame_BufferEmptyAtEnd()
    {
        byte[][] frames =
        [
            BuildFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize, 1),
            BuildFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize, 2),
            BuildFrame(HeartbeatRequest.Opcode, HeartbeatRequest.PayloadSize, 3)
        ];

        var concatenated = new byte[frames.Sum(f => f.Length)];
        var offset = 0;
        foreach (var frame in frames)
        {
            frame.CopyTo(concatenated, offset);
            offset += frame.Length;
        }

        var buffer = new ReadOnlySequence<byte>(concatenated);

        foreach (var expected in frames)
        {
            var decoded = FrameDecoder.TryReadFrame(ref buffer, Server, out var frame);

            Assert.True(decoded);
            Assert.Equal(HeartbeatRequest.Opcode, frame.Opcode);
            Assert.Equal(expected[LegacyHeaders.ClientPacketSize..], frame.Payload.ToArray());
        }

        Assert.Equal(0, buffer.Length);
    }

    [Theory]
    [InlineData(FenrirServer.Login)]
    [InlineData(FenrirServer.Zone)]
    public void TryReadFrame_UnregisteredOpcode_ThrowsProtocolViolationException_WithServerAndOpcode(
        FenrirServer server)
    {
        var header = new byte[LegacyHeaders.ClientPacketSize];
        header[8] = UnregisteredOpcode;
        var buffer = new ReadOnlySequence<byte>(header);

        var exception = Assert.Throws<ProtocolViolationException>(() =>
            FrameDecoder.TryReadFrame(ref buffer, server, out _));

        Assert.Equal(server, exception.Server);
        Assert.Equal(UnregisteredOpcode, exception.Opcode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(4)]
    [InlineData(8)] // one byte short of the 9-byte CLIENT_PACKET header
    public void TryReadFrame_BufferShorterThanHeader_ReturnsFalse_NeverThrows_NeverConsumes(int length)
    {
        var bytes = new byte[length];
        Array.Fill(bytes, (byte)0xFF); // hostile filler — must never be inspected before 9 bytes exist
        var buffer = new ReadOnlySequence<byte>(bytes);
        var originalLength = buffer.Length;

        var decoded = FrameDecoder.TryReadFrame(ref buffer, Server, out _);

        Assert.False(decoded);
        Assert.Equal(originalLength, buffer.Length);
    }

    [Fact]
    public void TryReadFrame_EmptyBuffer_ReturnsFalse_NeverThrows()
    {
        var buffer = ReadOnlySequence<byte>.Empty;

        var decoded = FrameDecoder.TryReadFrame(ref buffer, Server, out _);

        Assert.False(decoded);
        Assert.Equal(0, buffer.Length);
    }

    // The gate criterion for this phase: fragmentation at every single byte boundary must never corrupt or
    // drop a frame, and must never be mistaken for a complete one. ZoneHandshakeRequest (272 bytes total) is
    // built as an explicit two-segment ReadOnlySequenceSegment chain, re-cut at every position from 1 to 271 —
    // a flat byte[]-backed ReadOnlySequence can never exercise the header/payload-straddles-segment case that
    // a real Pipe delivers in production.
    [Fact]
    public void TryReadFrame_CzTempRegisterSend_FragmentedAtEveryByteBoundary_NeverCorruptsNeverMisfires()
    {
        var frameSize = LegacyHeaders.ClientPacketSize + ZoneHandshakeRequest.PayloadSize;
        var fullFrame = BuildFrame(ZoneHandshakeRequest.Opcode, ZoneHandshakeRequest.PayloadSize, 5);
        var expectedPayload = fullFrame[LegacyHeaders.ClientPacketSize..];

        var fragmentationCases = 0;
        for (var cut = 1; cut < frameSize; cut++)
        {
            // Incomplete: only the first `cut` of 272 bytes have arrived, split across two segments.
            // Must report "not enough yet" and leave the caller's buffer completely untouched.
            var incomplete = TwoSegments(fullFrame, cut, cut / 2);
            var incompleteLength = incomplete.Length;

            var decodedIncomplete = FrameDecoder.TryReadFrame(ref incomplete, Server, out _);

            Assert.False(decodedIncomplete);
            Assert.Equal(incompleteLength, incomplete.Length);

            // Complete: all 272 bytes are present, but the two-segment junction sits at this exact
            // boundary — sweeping `cut` through the whole frame forces the junction through the header,
            // through the payload, and through every point in between.
            var complete = TwoSegments(fullFrame, frameSize, cut);

            var decodedComplete = FrameDecoder.TryReadFrame(ref complete, Server, out var frame);

            Assert.True(decodedComplete);
            Assert.Equal(ZoneHandshakeRequest.Opcode, frame.Opcode);
            Assert.Equal(expectedPayload, frame.Payload.ToArray());
            Assert.Equal(0, complete.Length);

            fragmentationCases++;
        }

        Assert.Equal(frameSize - 1, fragmentationCases);
    }

    /// <summary>
    ///     Builds a raw <c>CLIENT_PACKET</c> frame: 9-byte header (opcode at offset 8) + a
    ///     deterministic, seed-derived payload — content is irrelevant to FrameDecoder, only distinct enough
    ///     to catch accidental byte-copy bugs.
    /// </summary>
    private static byte[] BuildFrame(byte opcode, int payloadSize, int seed)
    {
        var frame = new byte[LegacyHeaders.ClientPacketSize + payloadSize];

        // tPacket1/tPacket2 (offsets 0..7) carry no framing information in BuildEU33 — filled with
        // non-zero noise to prove FrameDecoder never looks at them.
        Array.Fill(frame, (byte)(seed | 0x80), 0, 8);
        frame[8] = opcode;

        for (var i = 0; i < payloadSize; i++)
            frame[LegacyHeaders.ClientPacketSize + i] = (byte)(seed + i);

        return frame;
    }

    /// <summary>
    ///     Splits <paramref name="data" />[0..<paramref name="length" />) into a genuine two-segment
    ///     <see cref="ReadOnlySequence{T}" /> at <paramref name="splitAt" /> — a plain
    ///     <c>
    ///         new
    ///         ReadOnlySequence&lt;byte&gt;(array)
    ///     </c>
    ///     is always single-segment and can never reproduce a
    ///     header/payload that straddles a pipe's segment boundary.
    /// </summary>
    private static ReadOnlySequence<byte> TwoSegments(byte[] data, int length, int splitAt)
    {
        var first = new Segment(data.AsMemory(0, splitAt));
        var last = first.Append(data.AsMemory(splitAt, length - splitAt));
        return new ReadOnlySequence<byte>(first, 0, last, last.Memory.Length);
    }

    private sealed class Segment : ReadOnlySequenceSegment<byte>
    {
        public Segment(ReadOnlyMemory<byte> memory)
        {
            Memory = memory;
        }

        public Segment Append(ReadOnlyMemory<byte> memory)
        {
            var next = new Segment(memory) { RunningIndex = RunningIndex + Memory.Length };
            Next = next;
            return next;
        }
    }
}
