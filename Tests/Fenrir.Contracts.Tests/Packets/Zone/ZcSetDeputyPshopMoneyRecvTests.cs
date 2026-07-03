using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcSetDeputyPshopMoneyRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, ZcSetDeputyPshopMoneyRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.SetDeputyPshopMoneyRecv, ZcSetDeputyPshopMoneyRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcSetDeputyPshopMoneyRecv { Result = 0, Money = 4000, BigMoney = 2 };

        var actual = new byte[ZcSetDeputyPshopMoneyRecv.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(ZcSetDeputyPshopMoneyRecv.PayloadSize, written);

        var expected = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 0);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 4000);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 2);

        Assert.Equal(expected, actual);
    }
}
