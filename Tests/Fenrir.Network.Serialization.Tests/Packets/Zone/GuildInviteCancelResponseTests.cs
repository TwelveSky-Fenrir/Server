using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

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
