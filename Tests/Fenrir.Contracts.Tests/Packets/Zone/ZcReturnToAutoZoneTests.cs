using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcReturnToAutoZoneTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        // ExpectedSize=1 (outbound header only) -> 0-byte payload: empty struct (ZONE.h:942-944).
        Assert.Equal(0, ZcReturnToAutoZone.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ReturnToAutoZone, ZcReturnToAutoZone.Opcode);
    }

    [Fact]
    public void Write_EmptyPayload_WritesZeroBytes()
    {
        // Outgoing-only packet: the generator emits Write but not TryRead (no IIncomingPacket<T> on this type).
        var packet = new ZcReturnToAutoZone();

        Span<byte> buffer = [];
        var written = packet.Write(buffer);

        Assert.Equal(0, written);
    }
}
