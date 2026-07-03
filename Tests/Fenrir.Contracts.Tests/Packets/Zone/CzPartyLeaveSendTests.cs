using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzPartyLeaveSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, CzPartyLeaveSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PartyLeaveSend, CzPartyLeaveSend.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = CzPartyLeaveSend.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new CzPartyLeaveSend(), packet);
    }
}
