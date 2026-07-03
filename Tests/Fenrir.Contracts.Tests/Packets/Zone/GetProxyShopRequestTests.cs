using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGetDeputyPshopSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(21, GetProxyShopRequest.PayloadSize);
        Assert.Equal(4 + 4 + 13, GetProxyShopRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GetProxyShop, GetProxyShopRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[GetProxyShopRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], -7);
        WireTestKit.WriteFixedString(buffer.Slice(8, 13), "Loki");

        var ok = GetProxyShopRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1, packet.Sort);
        Assert.Equal(-7, packet.UniqueNumber);
        Assert.Equal("Loki", packet.AvatarName);
    }
}
