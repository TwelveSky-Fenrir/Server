using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcDeputyPshopActionRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(64, ProxyShopStallStateResponse.PayloadSize);
        Assert.Equal(4 + 4 + ProxyStateInfo.WireSize + 2 + 4, ProxyShopStallStateResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ProxyShopStallState, ProxyShopStallStateResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var proxy = WireTestKit.CreatePopulated<ProxyStateInfo>(4);
        var packet = new ProxyShopStallStateResponse
        {
            ServerIndex = 37,
            UniqueNumber = 555,
            ProxyObject = proxy,
            CheckChangeActionState = 1
        };

        Span<byte> buffer = new byte[ProxyShopStallStateResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ProxyShopStallStateResponse.PayloadSize, written);
        Assert.Equal(37, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(555, BinaryPrimitives.ReadInt32LittleEndian(buffer[4..]));

        var ok = ProxyStateInfo.TryRead(buffer.Slice(8, ProxyStateInfo.WireSize), out var proxyBack);
        Assert.True(ok);
        WireTestKit.AssertDeepEqual(proxy, proxyBack);

        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(buffer[(8 + ProxyStateInfo.WireSize + 2)..]));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var proxy = new ProxyStateInfo
        {
            Location = [10f, 0f, 20f],
            Name = "Vidar",
            PshopName = "VidarStall"
        };
        var packet = new ProxyShopStallStateResponse
        {
            ServerIndex = 37,
            UniqueNumber = 42,
            ProxyObject = proxy,
            CheckChangeActionState = 0
        };

        var golden = new byte[ProxyShopStallStateResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 37);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 42);
        var proxyWritten = WireTestKit.EncodeProxyStateInfo(golden.AsSpan(8, ProxyStateInfo.WireSize), proxy);
        Assert.Equal(ProxyStateInfo.WireSize, proxyWritten);
        // 2 bytes of queue padding (offsets 58-59) are left zero by default.
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(8 + ProxyStateInfo.WireSize + 2), 0);

        Span<byte> buffer = new byte[ProxyShopStallStateResponse.PayloadSize];
        packet.Write(buffer);

        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
