using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class Zc194TypeBattleCountdownTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, Zc194TypeBattleCountdown.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.Zone194TypeBattleCountdown, Zc194TypeBattleCountdown.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new Zc194TypeBattleCountdown { RemainTime = 42 };

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 42);

        Assert.Equal(expected, actual);
    }
}
