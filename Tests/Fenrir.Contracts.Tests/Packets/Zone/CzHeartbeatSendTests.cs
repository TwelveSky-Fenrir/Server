using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzHeartbeatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(36, CzHeartbeatSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.HeartbeatSend, CzHeartbeatSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzHeartbeatSend.PayloadSize];
        BinaryPrimitives.WriteUInt32LittleEndian(buffer, 123_456_789u);
        var data = new byte[32];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(i + 1);
        data.AsSpan().CopyTo(buffer[4..]);

        var ok = CzHeartbeatSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(123_456_789u, packet.LastSend);
        Assert.True(data.AsSpan().SequenceEqual(packet.Data));
    }
}
