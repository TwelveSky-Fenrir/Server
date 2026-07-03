using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGuildAnswerRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcGuildAnswerRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildAnswerRecv, ZcGuildAnswerRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcGuildAnswerRecv { Answer = 4 };

        Span<byte> buffer = stackalloc byte[ZcGuildAnswerRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcGuildAnswerRecv.PayloadSize, written);
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
