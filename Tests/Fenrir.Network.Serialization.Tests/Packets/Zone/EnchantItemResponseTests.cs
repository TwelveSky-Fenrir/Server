using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

/// <summary>ZC_IMPROVE_ITEM_RECV (ZONE.h:513-518, 12-byte payload): Result/Cost/Value (source: <c>iValue</c>).</summary>
public class ZcImproveItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, EnchantItemResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.EnchantItem, EnchantItemResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new EnchantItemResponse { Result = 11, Cost = 22, Value = 33 };

        var actual = new byte[EnchantItemResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 11);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 22);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 33);

        Assert.Equal(expected, actual);
    }
}
