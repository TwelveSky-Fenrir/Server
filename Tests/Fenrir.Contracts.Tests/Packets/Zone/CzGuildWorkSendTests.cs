using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGuildWorkSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(504, CzGuildWorkSend.PayloadSize);
        Assert.Equal(4 + 500, CzGuildWorkSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildWorkSend, CzGuildWorkSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzGuildWorkSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 8);
        var data = new byte[500];
        for (var i = 0; i < data.Length; i++)
            data[i] = (byte)(i + 1);
        data.AsSpan().CopyTo(buffer[4..]);

        var ok = CzGuildWorkSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(8, packet.Sort);
        Assert.True(data.AsSpan().SequenceEqual(packet.Data));
    }
}
