using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

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
