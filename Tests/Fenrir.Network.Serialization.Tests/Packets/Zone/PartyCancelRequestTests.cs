using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzPartyCancelSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, PartyCancelRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PartyCancel, PartyCancelRequest.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = PartyCancelRequest.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new PartyCancelRequest(), packet);
    }
}
