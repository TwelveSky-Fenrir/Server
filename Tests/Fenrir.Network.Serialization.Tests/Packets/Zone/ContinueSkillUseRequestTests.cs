using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzContinueSkillUseSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(16, ContinueSkillUseRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ContinueSkillUse, ContinueSkillUseRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        var golden = new byte[16];
        BinaryPrimitives.WriteSingleLittleEndian(golden.AsSpan(0), 1.5f);
        BinaryPrimitives.WriteSingleLittleEndian(golden.AsSpan(4), 2.5f);
        BinaryPrimitives.WriteSingleLittleEndian(golden.AsSpan(8), 3.5f);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(12), 1);

        var ok = ContinueSkillUseRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal([1.5f, 2.5f, 3.5f], packet.Location);
        Assert.Equal(1, packet.Sort);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(ContinueSkillUseRequest.TryRead(new byte[15], out _));
    }
}
