using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcDrunkInfoRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, DrunkStateResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.DrunkState, DrunkStateResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new DrunkStateResponse { Sort = 1, Result = 0, BottleIndex = 3 };

        var actual = new byte[12];
        value.Write(actual);

        var expected = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 1);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 0);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 3);

        Assert.Equal(expected, actual);
    }
}
