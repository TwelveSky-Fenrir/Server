using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGuildAnswerRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, GuildInviteAnswerResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildInviteAnswer, GuildInviteAnswerResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new GuildInviteAnswerResponse { Answer = 4 };

        Span<byte> buffer = stackalloc byte[GuildInviteAnswerResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(GuildInviteAnswerResponse.PayloadSize, written);
        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
