using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>ZC_IMPROVE_ITEM_RECV (ZONE.h:513-518, 12-byte payload): Result/Cost/Value (source: <c>iValue</c>).</summary>
public class ZcImproveItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, ZcImproveItemRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ImproveItemRecv, ZcImproveItemRecv.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new ZcImproveItemRecv { Result = 11, Cost = 22, Value = 33 };

        var actual = new byte[ZcImproveItemRecv.PayloadSize];
        value.Write(actual);

        var expected = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 11);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 22);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 33);

        Assert.Equal(expected, actual);
    }
}
