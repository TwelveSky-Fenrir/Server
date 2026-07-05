using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcSetDeputyPshopMoneyRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, WithdrawProxyShopEarningsResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.WithdrawProxyShopEarnings, WithdrawProxyShopEarningsResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new WithdrawProxyShopEarningsResponse { Result = 0, Money = 4000, BigMoney = 2 };

        var actual = new byte[WithdrawProxyShopEarningsResponse.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(WithdrawProxyShopEarningsResponse.PayloadSize, written);

        var expected = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 0);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 4000);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 2);

        Assert.Equal(expected, actual);
    }
}
