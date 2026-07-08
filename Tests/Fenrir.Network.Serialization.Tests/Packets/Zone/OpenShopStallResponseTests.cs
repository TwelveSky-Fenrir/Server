using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcStartPshopRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(1236, OpenShopStallResponse.PayloadSize);
        Assert.Equal(4 + PshopInfo.WireSize, OpenShopStallResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.OpenShopStall, OpenShopStallResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var shop = WireTestKit.CreatePopulated<PshopInfo>(2);
        var packet = new OpenShopStallResponse { Result = 0, PshopInfo = shop };

        Span<byte> buffer = new byte[OpenShopStallResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(OpenShopStallResponse.PayloadSize, written);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buffer));

        var ok = PshopInfo.TryRead(buffer.Slice(4, PshopInfo.WireSize), out var shopBack);
        Assert.True(ok);
        WireTestKit.AssertDeepEqual(shop, shopBack);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var shop = WireTestKit.CreatePopulated<PshopInfo>(11);
        var packet = new OpenShopStallResponse { Result = 103, PshopInfo = shop };

        var golden = new byte[OpenShopStallResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 103);
        var shopWritten = WireTestKit.EncodePshopInfo(golden.AsSpan(4), shop);
        Assert.Equal(PshopInfo.WireSize, shopWritten);

        Span<byte> buffer = new byte[OpenShopStallResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
