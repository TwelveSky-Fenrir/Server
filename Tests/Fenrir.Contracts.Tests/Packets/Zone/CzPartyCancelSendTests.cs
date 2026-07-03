using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzPartyCancelSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, CzPartyCancelSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PartyCancelSend, CzPartyCancelSend.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = CzPartyCancelSend.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new CzPartyCancelSend(), packet);
    }
}
