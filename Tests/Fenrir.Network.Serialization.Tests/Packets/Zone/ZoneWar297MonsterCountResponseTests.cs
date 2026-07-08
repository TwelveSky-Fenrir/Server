using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class Zc297TypeRemainMonsterInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(16, ZoneWar297MonsterCountResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ZoneWar297MonsterCount, ZoneWar297MonsterCountResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[16];
        value.Write(actual);

        var expected = new byte[16];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static ZoneWar297MonsterCountResponse CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new ZoneWar297MonsterCountResponse { MonsterNum = v.NextIntArray(4) };
    }

    private static void EncodeGolden(Span<byte> destination, ZoneWar297MonsterCountResponse value)
    {
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(i * 4)..], value.MonsterNum[i]);
    }
}
