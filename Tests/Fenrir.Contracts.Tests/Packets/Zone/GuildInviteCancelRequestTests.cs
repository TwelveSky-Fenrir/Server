using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGuildCancelSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, GuildInviteCancelRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildInviteCancel, GuildInviteCancelRequest.Opcode);
    }

    [Fact]
    public void TryRead_EmptyBuffer_Succeeds()
    {
        var ok = GuildInviteCancelRequest.TryRead([], out _);

        Assert.True(ok);
    }
}
