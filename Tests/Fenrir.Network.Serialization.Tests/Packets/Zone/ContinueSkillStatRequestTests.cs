using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;
using Fenrir.Network.Serialization.Tests.TestSupport;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzContinueSkillStatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(64, ContinueSkillStatRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ContinueSkillStat, ContinueSkillStatRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        var v = new SequentialValueFactory();
        var skill = v.NextIntArray(16);

        var golden = new byte[64];
        for (var i = 0; i < 16; i++)
            BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(i * 4), skill[i]);

        var ok = ContinueSkillStatRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.True(skill.AsSpan().SequenceEqual(packet.Skill));
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(ContinueSkillStatRequest.TryRead(new byte[63], out _));
    }
}
