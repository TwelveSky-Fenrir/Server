using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcReturnToAutoZoneTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        // ExpectedSize=1 (outbound header only) -> 0-byte payload: empty struct (ZONE.h:942-944).
        Assert.Equal(0, ReturnToHomeZoneResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ReturnToHomeZone, ReturnToHomeZoneResponse.Opcode);
    }

    [Fact]
    public void Write_EmptyPayload_WritesZeroBytes()
    {
        // Outgoing-only packet: the generator emits Write but not TryRead (no IIncomingPacket<T> on this type).
        var packet = new ReturnToHomeZoneResponse();

        Span<byte> buffer = [];
        var written = packet.Write(buffer);

        Assert.Equal(0, written);
    }
}
