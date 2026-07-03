using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGuildCancelRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, GuildInviteCancelResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildInviteCancel, GuildInviteCancelResponse.Opcode);
    }

    [Fact]
    public void Write_ReturnsZero()
    {
        var packet = new GuildInviteCancelResponse();

        var written = packet.Write([]);

        Assert.Equal(0, written);
    }
}
