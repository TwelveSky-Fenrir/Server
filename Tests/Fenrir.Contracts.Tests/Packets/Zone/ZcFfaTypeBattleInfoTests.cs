using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcFfaTypeBattleInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcFfaTypeBattleInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FfaTypeBattleInfo, ZcFfaTypeBattleInfo.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new ZcFfaTypeBattleInfo { RemainTime = 15 };

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 15);

        Assert.Equal(expected, actual);
    }
}
