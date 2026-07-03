using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class Zc241TypeBattleInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, Zc241TypeBattleInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.Zone241TypeBattleInfo, Zc241TypeBattleInfo.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new Zc241TypeBattleInfo { RemainTime = 7 };

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 7);

        Assert.Equal(expected, actual);
    }
}
