using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcBuyBloodMarkRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(40, ZcBuyBloodMarkRecv.PayloadSize);
        Assert.Equal(4 + 4 + 4 + 4 + 24, ZcBuyBloodMarkRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.BuyBloodMarkRecv, ZcBuyBloodMarkRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var value = new int[6];
        for (var i = 0; i < value.Length; i++)
            value[i] = (i + 1) * 9;

        var packet = new ZcBuyBloodMarkRecv
        {
            Result = 0,
            BloodCoin = 250,
            Page1 = 2,
            Index1 = 4,
            Value = value
        };

        var actual = new byte[ZcBuyBloodMarkRecv.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(ZcBuyBloodMarkRecv.PayloadSize, written);

        var expected = new byte[ZcBuyBloodMarkRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 0);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 250);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 2);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(12), 4);
        for (var i = 0; i < value.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(16 + i * 4), value[i]);

        Assert.Equal(expected, actual);
    }
}
