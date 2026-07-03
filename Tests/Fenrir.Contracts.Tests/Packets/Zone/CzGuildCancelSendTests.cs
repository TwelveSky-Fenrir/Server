using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGuildCancelSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, CzGuildCancelSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildCancelSend, CzGuildCancelSend.Opcode);
    }

    [Fact]
    public void TryRead_EmptyBuffer_Succeeds()
    {
        var ok = CzGuildCancelSend.TryRead([], out _);

        Assert.True(ok);
    }
}
