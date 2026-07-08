using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzStartPshopSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(1236, OpenShopStallRequest.PayloadSize);
        Assert.Equal(4 + PshopInfo.WireSize, OpenShopStallRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.OpenShopStall, OpenShopStallRequest.Opcode);
    }

    [Fact]
    public void TryRead_RoundTrips_ThroughPshopInfoWrite()
    {
        var shop = WireTestKit.CreatePopulated<PshopInfo>(1);

        Span<byte> buffer = new byte[OpenShopStallRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1);
        var written = shop.Write(buffer[4..]);

        Assert.Equal(PshopInfo.WireSize, written);

        var ok = OpenShopStallRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.Sort);
        WireTestKit.AssertDeepEqual(shop, packet.PshopInfo);
    }

    [Fact]
    public void TryRead_GoldenBytes_DecodesEveryField()
    {
        var shop = WireTestKit.CreatePopulated<PshopInfo>(7);

        var golden = new byte[OpenShopStallRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 2);
        var shopWritten = WireTestKit.EncodePshopInfo(golden.AsSpan(4), shop);
        Assert.Equal(PshopInfo.WireSize, shopWritten);

        var ok = OpenShopStallRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal(2, packet.Sort);
        WireTestKit.AssertDeepEqual(shop, packet.PshopInfo);
    }
}
