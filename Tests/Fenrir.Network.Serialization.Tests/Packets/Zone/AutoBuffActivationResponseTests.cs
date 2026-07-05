using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcContinueSkillUseRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, AutoBuffActivationResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.AutoBuffActivation, AutoBuffActivationResponse.Opcode);
    }

    [Fact]
    public void Write_MatchesGoldenBytes()
    {
        var value = new AutoBuffActivationResponse { Value = 5 };

        var actual = new byte[4];
        value.Write(actual);

        var expected = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(expected, 5);

        Assert.Equal(expected, actual);
    }
}
