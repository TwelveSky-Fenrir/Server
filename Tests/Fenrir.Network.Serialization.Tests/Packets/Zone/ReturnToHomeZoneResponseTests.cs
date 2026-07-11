using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcReturnToAutoZoneTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, ReturnToHomeZoneResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ReturnToHomeZone, ReturnToHomeZoneResponse.Opcode);
    }

    [Fact]
    public void Write_EmptyPayload_WritesZeroBytes()
    {
        var packet = new ReturnToHomeZoneResponse();

        Span<byte> buffer = [];
        var written = packet.Write(buffer);

        Assert.Equal(0, written);
    }
}
