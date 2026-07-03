using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGuildCancelRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, ZcGuildCancelRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildCancelRecv, ZcGuildCancelRecv.Opcode);
    }

    [Fact]
    public void Write_ReturnsZero()
    {
        var packet = new ZcGuildCancelRecv();

        var written = packet.Write([]);

        Assert.Equal(0, written);
    }
}
