using System.Buffers;
using Fenrir.Contracts.Abstractions;
using Fenrir.Network.Framing;

namespace Fenrir.Application.Login.Tests.TestSupport;

/// <summary>
///     Reads what a handler actually put on the wire and compares it against the EXACT frame the same
///     <see cref="FrameWriter" /> the production <c>ClientSession.Send</c> path uses would produce for a hand-built
///     expected packet — same idea as Fenrir.Network.Tests' <c>ClientSessionSendTests</c>, lifted to the handler
///     level so each handler test asserts on values, not on manually recomputed offsets.
/// </summary>
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
