using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcContinueSkillStatRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, AutoBuffRegisterResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.AutoBuffRegister, AutoBuffRegisterResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new AutoBuffRegisterResponse { Value = 1 };

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 1);

        Assert.Equal(expected, actual);
    }
}
