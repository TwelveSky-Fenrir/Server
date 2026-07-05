using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzGuildWorkSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(504, GuildActionRequest.PayloadSize);
        Assert.Equal(4 + 500, GuildActionRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildAction, GuildActionRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[GuildActionRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 8);
        var data = new byte[500];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(i + 1);
        data.AsSpan().CopyTo(buffer[4..]);

        var ok = GuildActionRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(8, packet.Sort);
        Assert.True(data.AsSpan().SequenceEqual(packet.Data));
    }
}
