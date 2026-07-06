using System.Buffers;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Framing;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Tests.Framing;

public class FrameDecoderTests
{
    private const FenrirServer Server = FenrirServer.Zone;

    // Login Incoming tops out at 25, Zone Incoming at 151 -- 250 is registered nowhere.
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
        Assert.Equal(frameBytes[WireHeaderSizes.ClientPacketSize..], frame.Payload.ToArray());
        Assert.Equal(0, buffer.Length);
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
            Assert.Equal(expected[WireHeaderSizes.ClientPacketSize..], frame.Payload.ToArray());
        }

        Assert.Equal(0, buffer.Length);
    }

    [Theory]
    [InlineData(FenrirServer.Login)]
    [InlineData(FenrirServer.Zone)]
    public void TryReadFrame_UnregisteredOpcode_ThrowsProtocolViolationException_WithServerAndOpcode(
        FenrirServer server)
    {
        var header = new byte[WireHeaderSizes.ClientPacketSize];
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
        Array.Fill(bytes, (byte)0xFF); // must never be inspected before 9 bytes exist
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

    // Fragmentation at every byte boundary must never corrupt or drop a frame. A flat byte[]-backed
    // ReadOnlySequence is always single-segment, so this builds an explicit two-segment chain, re-cut at
    // every position, to reproduce the header/payload-straddles-segment case a real Pipe delivers.
    [Fact]
    public void TryReadFrame_CzTempRegisterSend_FragmentedAtEveryByteBoundary_NeverCorruptsNeverMisfires()
    {
        var frameSize = WireHeaderSizes.ClientPacketSize + ZoneHandshakeRequest.PayloadSize;
        var fullFrame = BuildFrame(ZoneHandshakeRequest.Opcode, ZoneHandshakeRequest.PayloadSize, 5);
        var expectedPayload = fullFrame[WireHeaderSizes.ClientPacketSize..];

        var fragmentationCases = 0;
        for (var cut = 1; cut < frameSize; cut++)
        {
            var incomplete = TwoSegments(fullFrame, cut, cut / 2);
            var incompleteLength = incomplete.Length;

            var decodedIncomplete = FrameDecoder.TryReadFrame(ref incomplete, Server, out _);

            Assert.False(decodedIncomplete);
            Assert.Equal(incompleteLength, incomplete.Length);

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

    private static byte[] BuildFrame(byte opcode, int payloadSize, int seed)
    {
        var frame = new byte[WireHeaderSizes.ClientPacketSize + payloadSize];

        // tPacket1/tPacket2 (offsets 0..7) carry no framing info in BuildEU33 -- noise proves FrameDecoder
        // never looks at them.
        Array.Fill(frame, (byte)(seed | 0x80), 0, 8);
        frame[8] = opcode;

        for (var i = 0; i < payloadSize; i++)
            frame[WireHeaderSizes.ClientPacketSize + i] = (byte)(seed + i);

        return frame;
    }

    // Splits data[0..length) into a genuine two-segment ReadOnlySequence at splitAt.
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
