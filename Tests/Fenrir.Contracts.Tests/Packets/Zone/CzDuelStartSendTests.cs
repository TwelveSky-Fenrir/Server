using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzDuelStartSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, CzDuelStartSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.DuelStartSend, CzDuelStartSend.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = CzDuelStartSend.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new CzDuelStartSend(), packet);
    }
}
