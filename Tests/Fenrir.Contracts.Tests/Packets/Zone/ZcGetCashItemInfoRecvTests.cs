using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     ZC_GET_CASH_ITEM_INFO_RECV (ZONE.h:1036-1041, 12 809 bytes) — the largest packet in the protocol.
///     <c>CashItemInfo</c> flattens
///     <c>
///         int[MAX_CASH_TYPE=4][MAX_CASH_PAGE=20][MAX_CASH_ITEM_PER_PAGE=10]
///         [MAX_CASH_ITEM_DETIAL=4]
///     </c>
///     row-major into 3200 ints (STRUCT.h:1436-1446, CASH_INFO_SIZE).
/// </summary>
public class ZcGetCashItemInfoRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12808, ZcGetCashItemInfoRecv.PayloadSize);
        Assert.Equal(4 + 4 + 3200 * 4, ZcGetCashItemInfoRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GetCashItemInfoRecv, ZcGetCashItemInfoRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var cashItemInfo = new int[3200];
        for (var i = 0; i < cashItemInfo.Length; i++)
            cashItemInfo[i] = i + 1;

        var packet = new ZcGetCashItemInfoRecv
        {
            Result = 0,
            Version = 7,
            CashItemInfo = cashItemInfo
        };

        var actual = new byte[ZcGetCashItemInfoRecv.PayloadSize];
        var written = packet.Write(actual);
        Assert.Equal(ZcGetCashItemInfoRecv.PayloadSize, written);

        var expected = new byte[ZcGetCashItemInfoRecv.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 0);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 7);
        for (var i = 0; i < cashItemInfo.Length; i++)
            BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8 + i * 4), cashItemInfo[i]);

        Assert.Equal(expected, actual);
    }
}
