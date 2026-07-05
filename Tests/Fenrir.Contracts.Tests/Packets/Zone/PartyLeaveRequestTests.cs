using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzPartyLeaveSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, PartyLeaveRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PartyLeave, PartyLeaveRequest.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = PartyLeaveRequest.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new PartyLeaveRequest(), packet);
    }
}
