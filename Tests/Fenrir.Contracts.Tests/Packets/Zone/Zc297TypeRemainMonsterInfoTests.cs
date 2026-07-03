using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;
using Fenrir.Contracts.Tests.TestSupport;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class Zc297TypeRemainMonsterInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(16, Zc297TypeRemainMonsterInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.Zone297TypeRemainMonsterInfo, Zc297TypeRemainMonsterInfo.Opcode);
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

    private static Zc297TypeRemainMonsterInfo CreatePopulated()
    {
        var v = new SequentialValueFactory();
        return new Zc297TypeRemainMonsterInfo { MonsterNum = v.NextIntArray(4) };
    }

    private static void EncodeGolden(Span<byte> destination, Zc297TypeRemainMonsterInfo value)
    {
        for (var i = 0; i < 4; i++)
            BinaryPrimitives.WriteInt32LittleEndian(destination[(i * 4)..], value.MonsterNum[i]);
    }
}
