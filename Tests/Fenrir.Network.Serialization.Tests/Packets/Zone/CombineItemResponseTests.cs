using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

/// <summary>ZC_ADD_ITEM_RECV (ZONE.h:520-524, 8-byte payload) — only forge response with no trailing <c>tValue[6]</c>.</summary>
public class ZcAddItemRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, CombineItemResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.CombineItem, CombineItemResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new CombineItemResponse { Result = 11, Cost = 22 };

        var actual = new byte[CombineItemResponse.PayloadSize];
        value.Write(actual);

        var expected = new byte[8];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 11);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 22);

        Assert.Equal(expected, actual);
    }
}
