using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class Zc194TypeBattleCountdownTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZoneWar194CountdownResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ZoneWar194Countdown, ZoneWar194CountdownResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new ZoneWar194CountdownResponse { RemainTime = 42 };

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 42);

        Assert.Equal(expected, actual);
    }
}
