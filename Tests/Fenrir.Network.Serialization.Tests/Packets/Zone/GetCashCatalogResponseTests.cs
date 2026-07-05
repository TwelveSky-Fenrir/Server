using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

// CashItemInfo flattens int[4][20][10][4] row-major into 3200 ints (STRUCT.h:1436-1446).
public class ZcGetCashItemInfoRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12808, GetCashCatalogResponse.PayloadSize);
        Assert.Equal(4 + 4 + 3200 * 4, GetCashCatalogResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GetCashCatalog, GetCashCatalogResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var cashItemInfo = new int[3200];
        for (var i = 0; i < cashItemInfo.Length; i++)
            cashItemInfo[i] = i + 1;

        var packet = new GetCashCatalogResponse
        {
            Result = 0,
            Version = 7,
            CashItemInfo = cashItemInfo
        };

        var actual = new byte[GetCashCatalogResponse.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(GetCashCatalogResponse.PayloadSize, written);

        var expected = new byte[GetCashCatalogResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 0);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 7);
        for (var i = 0; i < cashItemInfo.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8 + i * 4), cashItemInfo[i]);

        Assert.Equal(expected, actual);
    }
}
