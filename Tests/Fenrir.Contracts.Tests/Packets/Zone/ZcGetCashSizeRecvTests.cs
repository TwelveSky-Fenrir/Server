using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGetCashSizeRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, ZcGetCashSizeRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GetCashSizeRecv, ZcGetCashSizeRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcGetCashSizeRecv { CashSize = 15_000, Sort = 1 };

        var actual = new byte[ZcGetCashSizeRecv.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(ZcGetCashSizeRecv.PayloadSize, written);

        var expected = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 15_000);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 1);

        Assert.Equal(expected, actual);
    }
}
