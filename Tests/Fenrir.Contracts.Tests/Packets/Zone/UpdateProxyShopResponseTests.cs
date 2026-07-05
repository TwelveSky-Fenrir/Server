using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcSetDeputyPshopRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(876, UpdateProxyShopResponse.PayloadSize);
        Assert.Equal(4 + ProxyShopUserInfo.WireSize + 4 + 4 + 36 + 4, UpdateProxyShopResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.UpdateProxyShop, UpdateProxyShopResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var proxyUser = WireTestKit.CreatePopulated<ProxyShopUserInfo>(6);
        var value1 = new int[9];
        for (var i = 0; i < value1.Length; i++)
            value1[i] = (i + 1) * 7;

        var packet = new UpdateProxyShopResponse
        {
            Result = 0,
            ProxyUser = proxyUser,
            Page = 2,
            Index = 3,
            Value1 = value1,
            Money = 12_000
        };

        Span<byte> buffer = new byte[UpdateProxyShopResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(UpdateProxyShopResponse.PayloadSize, written);
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buffer));

        var ok = ProxyShopUserInfo.TryRead(buffer.Slice(4, ProxyShopUserInfo.WireSize), out var proxyUserBack);
        Assert.True(ok);
        WireTestKit.AssertDeepEqual(proxyUser, proxyUserBack);

        var afterProxy = 4 + ProxyShopUserInfo.WireSize;
        Assert.Equal(2, BinaryPrimitives.ReadInt32LittleEndian(buffer[afterProxy..]));
        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(buffer[(afterProxy + 4)..]));
        for (var i = 0; i < value1.Length; i++)
            Assert.Equal(value1[i], BinaryPrimitives.ReadInt32LittleEndian(buffer[(afterProxy + 8 + i * 4)..]));
        Assert.Equal(12_000, BinaryPrimitives.ReadInt32LittleEndian(buffer[(afterProxy + 8 + 36)..]));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var proxyUser = WireTestKit.CreatePopulated<ProxyShopUserInfo>(31);
        var value1 = new int[9];
        for (var i = 0; i < value1.Length; i++)
            value1[i] = 1000 + i;

        var packet = new UpdateProxyShopResponse
        {
            Result = 1,
            ProxyUser = proxyUser,
            Page = 4,
            Index = 1,
            Value1 = value1,
            Money = 500
        };

        var golden = new byte[UpdateProxyShopResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 1);
        var proxyWritten = WireTestKit.EncodeProxyShopUserInfo(golden.AsSpan(4, ProxyShopUserInfo.WireSize), proxyUser);
        Assert.Equal(ProxyShopUserInfo.WireSize, proxyWritten);

        var offset = 4 + ProxyShopUserInfo.WireSize;
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(offset), 4);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(offset + 4), 1);
        for (var i = 0; i < value1.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(offset + 8 + i * 4), value1[i]);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(offset + 8 + 36), 500);

        Span<byte> buffer = new byte[UpdateProxyShopResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
