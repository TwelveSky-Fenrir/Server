using System.Buffers;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Framing;

namespace Fenrir.Application.Login.Tests.TestSupport;

internal static class PacketAssert
{
    public static async Task<byte[]> ReadSentBytesAsync(FakeDuplexPipe pipe)
    {
        var result = await pipe.SessionToPeer.ReadAsync();
        var bytes = result.Buffer.ToArray();
        pipe.SessionToPeer.AdvanceTo(result.Buffer.End);
        return bytes;
    }

    public static async Task AssertSentAsync<TPacket>(FakeDuplexPipe pipe, TPacket expected)
        where TPacket : struct, IOutgoingPacket
    {
        var actual = await ReadSentBytesAsync(pipe);
        var buffer = new byte[FrameWriter.FrameSizeOf<TPacket>()];
        FrameWriter.WriteFrame(in expected, buffer);
        Assert.Equal(buffer, actual);
    }

    public static void AssertNothingSent(FakeDuplexPipe pipe)
    {
        Assert.False(pipe.SessionToPeer.TryRead(out _));
    }
}
