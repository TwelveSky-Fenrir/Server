using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class Zc267TypeBattleInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, Zc267TypeBattleInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.Zone267TypeBattleInfo, Zc267TypeBattleInfo.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = CreatePopulated();

        var actual = new byte[20];
        value.Write(actual);

        var expected = new byte[20];
        EncodeGolden(expected, value);

        Assert.Equal(expected, actual);
    }

    private static Zc267TypeBattleInfo CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new Zc267TypeBattleInfo
        {
            BattleInfo = v.NextIntArray(4),
            RemainTime = v.NextInt()
        };
    }

    private static void EncodeGolden(Span<byte> destination, Zc267TypeBattleInfo value)
    {
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(i * 4)..], value.BattleInfo[i]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.RemainTime);
    }
}
