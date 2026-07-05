using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzPartyBreakSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, PartyDisbandRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PartyDisband, PartyDisbandRequest.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = PartyDisbandRequest.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new PartyDisbandRequest(), packet);
    }
}
