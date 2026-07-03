using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcDemandBloodMarkRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(604, ZcDemandBloodMarkRecv.PayloadSize);
        Assert.Equal(BloodShop.WireSize, ZcDemandBloodMarkRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DemandBloodMarkRecv, ZcDemandBloodMarkRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var shop = WireTestKit.CreatePopulated<BloodShop>(8);
        var packet = new ZcDemandBloodMarkRecv { Data = shop };

        Span<byte> buffer = new byte[ZcDemandBloodMarkRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcDemandBloodMarkRecv.PayloadSize, written);

        var ok = BloodShop.TryRead(buffer, out var shopBack);
        Assert.True(ok);
        WireTestKit.AssertDeepEqual(shop, shopBack);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var shop = WireTestKit.CreatePopulated<BloodShop>(17);
        var packet = new ZcDemandBloodMarkRecv { Data = shop };

        var golden = new byte[ZcDemandBloodMarkRecv.PayloadSize];
        var written = WireTestKit.EncodeBloodShop(golden, shop);
        Assert.Equal(BloodShop.WireSize, written);

        Span<byte> buffer = new byte[ZcDemandBloodMarkRecv.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
