using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGuildFindRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcGuildFindRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildFindRecv, ZcGuildFindRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcGuildFindRecv { Result = 17 };

        Span<byte> buffer = stackalloc byte[ZcGuildFindRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcGuildFindRecv.PayloadSize, written);
        Assert.Equal(17, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
