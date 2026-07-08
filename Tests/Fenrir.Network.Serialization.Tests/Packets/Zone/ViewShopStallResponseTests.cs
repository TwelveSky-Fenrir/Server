using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcDemandPshopRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(1236, ViewShopStallResponse.PayloadSize);
        Assert.Equal(4 + PshopInfo.WireSize, ViewShopStallResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ViewShopStall, ViewShopStallResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var shop = WireTestKit.CreatePopulated<PshopInfo>(3);
        var packet = new ViewShopStallResponse { Result = 1, PshopInfo = shop };

        Span<byte> buffer = new byte[ViewShopStallResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ViewShopStallResponse.PayloadSize, written);
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(buffer));

        var ok = PshopInfo.TryRead(buffer.Slice(4, PshopInfo.WireSize), out var shopBack);
        Assert.True(ok);
        WireTestKit.AssertDeepEqual(shop, shopBack);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var shop = WireTestKit.CreatePopulated<PshopInfo>(13);
        var packet = new ViewShopStallResponse { Result = 0, PshopInfo = shop };

        var golden = new byte[ViewShopStallResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 0);
        var shopWritten = WireTestKit.EncodePshopInfo(golden.AsSpan(4), shop);
        Assert.Equal(PshopInfo.WireSize, shopWritten);

        Span<byte> buffer = new byte[ViewShopStallResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
