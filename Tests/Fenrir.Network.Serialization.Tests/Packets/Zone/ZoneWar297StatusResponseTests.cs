using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class Zc297TypeRemainInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, ZoneWar297StatusResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ZoneWar297Status, ZoneWar297StatusResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new ZoneWar297StatusResponse { Value00 = 1, Value01 = 2, Value02 = 3 };

        var actual = new byte[12];
        value.Write(actual);

        var expected = new byte[12];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 1);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(4), 2);
        BinaryPrimitives.WriteInt32LittleEndian(expected.AsSpan(8), 3);

        Assert.Equal(expected, actual);
    }
}
