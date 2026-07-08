using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcPartyCancelRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, PartyCancelResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyCancel, PartyCancelResponse.Opcode);
    }

    [Fact]
    public void Write_EmptyPayload_ReturnsZero()
    {
        var packet = new PartyCancelResponse();

        Span<byte> buffer = [];
        var written = packet.Write(buffer);

        Assert.Equal(0, written);
    }
}
