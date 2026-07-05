using System.Buffers;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Framing;

namespace Fenrir.Application.Login.Tests.TestSupport;

// Compares what a handler put on the wire against the exact frame FrameWriter would produce for a
// hand-built expected packet, so handler tests assert on values, not manually recomputed offsets.
internal static class PacketAssert
{
    /// <summary>Drains exactly one pending read from the pipe (fails loudly if nothing was written at all).</summary>
    public static async Task<byte[]> ReadSentBytesAsync(FakeDuplexPipe pipe)
    {
        var result = await pipe.SessionToPeer.ReadAsync();
        var bytes = result.Buffer.ToArray();
        pipe.SessionToPeer.AdvanceTo(result.Buffer.End);
        return bytes;
    }

    /// <summary>
    ///     Asserts the session sent exactly the bytes <paramref name="expected" /> serializes to (opcode + payload + any
    ///     whole-frame XOR).
    /// </summary>
    public static async Task AssertSentAsync<TPacket>(FakeDuplexPipe pipe, TPacket expected)
        where TPacket : struct, IOutgoingPacket
    {
        var actual = await ReadSentBytesAsync(pipe);
        var buffer = new byte[FrameWriter.FrameSizeOf<TPacket>()];
        FrameWriter.WriteFrame(in expected, buffer);
        Assert.Equal(buffer, actual);
    }

    /// <summary>Asserts the handler replied nothing at all (legacy Quit()/no-reply paths).</summary>
    public static void AssertNothingSent(FakeDuplexPipe pipe)
    {
        Assert.False(pipe.SessionToPeer.TryRead(out _));
    }
}
