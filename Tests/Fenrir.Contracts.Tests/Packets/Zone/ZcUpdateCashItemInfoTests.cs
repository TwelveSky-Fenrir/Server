using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcUpdateCashItemInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        // ExpectedSize=1 (outbound opcode byte only) -> 0-byte payload: empty struct.
        Assert.Equal(0, ZcUpdateCashItemInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.UpdateCashItemInfo, ZcUpdateCashItemInfo.Opcode);
    }

    [Fact]
    public void Write_EmptyPayload_WritesZeroBytes()
    {
        var packet = new ZcUpdateCashItemInfo();

        var written = packet.Write(Span<byte>.Empty);

        Assert.Equal(0, written);
    }
}
