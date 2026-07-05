using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class Zc241TypeBattleInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZoneWar241StatusResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ZoneWar241Status, ZoneWar241StatusResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new ZoneWar241StatusResponse { RemainTime = 7 };

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 7);

        Assert.Equal(expected, actual);
    }
}
