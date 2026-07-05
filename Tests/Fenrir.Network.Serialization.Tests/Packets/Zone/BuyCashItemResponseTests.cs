using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcBuyCashItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(40, BuyCashItemResponse.PayloadSize);
        Assert.Equal(4 + 4 + 4 + 4 + 24, BuyCashItemResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.BuyCashItem, BuyCashItemResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var value = new int[6];
        for (var i = 0; i < value.Length; i++)
            value[i] = (i + 1) * 5;

        var packet = new BuyCashItemResponse
        {
            Result = 60704,
            CashSize = 900,
            Page = 1,
            Index = 3,
            Value = value
        };

        var actual = new byte[BuyCashItemResponse.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(BuyCashItemResponse.PayloadSize, written);

        var expected = new byte[BuyCashItemResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 60704);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 900);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 1);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(12), 3);
        for (var i = 0; i < value.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(16 + i * 4), value[i]);

        Assert.Equal(expected, actual);
    }
}
