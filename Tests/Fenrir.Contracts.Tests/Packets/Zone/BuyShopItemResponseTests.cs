using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcBuyPshopRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(52, BuyShopItemResponse.PayloadSize);
        Assert.Equal(4 + 4 + 4 + 4 + 24 + 12, BuyShopItemResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.BuyShopItem, BuyShopItemResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var value = new int[6];
        for (var i = 0; i < value.Length; i++)
            value[i] = (i + 1) * 3;
        var socket = new[] { 9, 8, 7 };

        var packet = new BuyShopItemResponse
        {
            Result = 0,
            Cost = 500,
            Page = 1,
            Index = 2,
            Value = value,
            Socket = socket
        };

        var actual = new byte[BuyShopItemResponse.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(BuyShopItemResponse.PayloadSize, written);

        var expected = new byte[BuyShopItemResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 0);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 500);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 1);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(12), 2);
        for (var i = 0; i < value.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(16 + i * 4), value[i]);
        for (var i = 0; i < socket.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(40 + i * 4), socket[i]);

        Assert.Equal(expected, actual);
    }
}
