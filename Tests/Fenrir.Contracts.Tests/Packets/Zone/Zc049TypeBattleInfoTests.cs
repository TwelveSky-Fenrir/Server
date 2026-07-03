using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class Zc049TypeBattleInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, Zc049TypeBattleInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.Zone049TypeBattleInfo, Zc049TypeBattleInfo.Opcode);
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

    private static Zc049TypeBattleInfo CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new Zc049TypeBattleInfo
        {
            TribeUserNum = v.NextIntArray(4),
            RemainTime = v.NextInt()
        };
    }

    private static void EncodeGolden(Span<byte> destination, Zc049TypeBattleInfo value)
    {
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(i * 4)..], value.TribeUserNum[i]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.RemainTime);
    }
}
