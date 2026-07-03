using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class Zc194TypeBattleInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(20, Zc194TypeBattleInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.Zone194TypeBattleInfo, Zc194TypeBattleInfo.Opcode);
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

    private static Zc194TypeBattleInfo CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new Zc194TypeBattleInfo
        {
            BattleInfo = v.NextIntArray(4),
            RemainTime = v.NextInt()
        };
    }

    private static void EncodeGolden(Span<byte> destination, Zc194TypeBattleInfo value)
    {
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(i * 4)..], value.BattleInfo[i]);
        BinaryPrimitives.WriteInt32LittleEndian(destination[16..], value.RemainTime);
    }
}
