using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcContinueSkillUseRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, ZcContinueSkillUseRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.ContinueSkillUseRecv, ZcContinueSkillUseRecv.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new ZcContinueSkillUseRecv { Value = 5 };

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 5);

        Assert.Equal(expected, actual);
    }
}
