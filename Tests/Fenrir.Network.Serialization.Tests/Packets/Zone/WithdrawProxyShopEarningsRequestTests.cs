using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzSetDeputyPshopMoneySendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, WithdrawProxyShopEarningsRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.WithdrawProxyShopEarnings, WithdrawProxyShopEarningsRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[WithdrawProxyShopEarningsRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 1000);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 5);

        var ok = WithdrawProxyShopEarningsRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(1000, packet.Money);
        Assert.Equal(5, packet.BigMoney);
    }
}
