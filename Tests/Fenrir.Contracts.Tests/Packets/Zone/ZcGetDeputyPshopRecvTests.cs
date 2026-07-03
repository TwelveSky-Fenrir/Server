using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGetDeputyPshopRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(832, ZcGetDeputyPshopRecv.PayloadSize);
        Assert.Equal(4 + 4 + ProxyShopUserInfo.WireSize, ZcGetDeputyPshopRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GetDeputyPshopRecv, ZcGetDeputyPshopRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var proxyUser = WireTestKit.CreatePopulated<ProxyShopUserInfo>(5);
        var packet = new ZcGetDeputyPshopRecv { Result = 0, Sort = 1, ProxyUser = proxyUser };

        Span<byte> buffer = new byte[ZcGetDeputyPshopRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcGetDeputyPshopRecv.PayloadSize, written);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(buffer[4..]));

        var ok = ProxyShopUserInfo.TryRead(buffer.Slice(8, ProxyShopUserInfo.WireSize), out var proxyUserBack);
        Assert.True(ok);
        WireTestKit.AssertDeepEqual(proxyUser, proxyUserBack);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var proxyUser = WireTestKit.CreatePopulated<ProxyShopUserInfo>(21);
        var packet = new ZcGetDeputyPshopRecv { Result = 2, Sort = -1, ProxyUser = proxyUser };

        var golden = new byte[ZcGetDeputyPshopRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 2);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), -1);
        var proxyWritten = WireTestKit.EncodeProxyShopUserInfo(golden.AsSpan(8, ProxyShopUserInfo.WireSize), proxyUser);
        Assert.Equal(ProxyShopUserInfo.WireSize, proxyWritten);

        Span<byte> buffer = new byte[ZcGetDeputyPshopRecv.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
