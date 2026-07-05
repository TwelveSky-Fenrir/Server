using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcGetCashSizeRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, GetCashBalanceResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GetCashBalance, GetCashBalanceResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new GetCashBalanceResponse { CashSize = 15_000, Sort = 1 };

        var actual = new byte[GetCashBalanceResponse.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(GetCashBalanceResponse.PayloadSize, written);

        var expected = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 15_000);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 1);

        Assert.Equal(expected, actual);
    }
}
