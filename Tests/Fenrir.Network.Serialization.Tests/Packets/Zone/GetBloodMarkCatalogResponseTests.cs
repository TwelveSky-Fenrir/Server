using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcDemandBloodMarkRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(604, GetBloodMarkCatalogResponse.PayloadSize);
        Assert.Equal(BloodShop.WireSize, GetBloodMarkCatalogResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GetBloodMarkCatalog, GetBloodMarkCatalogResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var shop = WireTestKit.CreatePopulated<BloodShop>(8);
        var packet = new GetBloodMarkCatalogResponse { Data = shop };

        Span<byte> buffer = new byte[GetBloodMarkCatalogResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(GetBloodMarkCatalogResponse.PayloadSize, written);

        var ok = BloodShop.TryRead(buffer, out var shopBack);
        Assert.True(ok);
        WireTestKit.AssertDeepEqual(shop, shopBack);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var shop = WireTestKit.CreatePopulated<BloodShop>(17);
        var packet = new GetBloodMarkCatalogResponse { Data = shop };

        var golden = new byte[GetBloodMarkCatalogResponse.PayloadSize];
        var written = WireTestKit.EncodeBloodShop(golden, shop);
        Assert.Equal(BloodShop.WireSize, written);

        Span<byte> buffer = new byte[GetBloodMarkCatalogResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
