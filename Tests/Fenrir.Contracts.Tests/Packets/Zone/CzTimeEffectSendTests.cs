using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTimeEffectSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzTimeEffectSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TimeEffectSend, CzTimeEffectSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzTimeEffectSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 3);

        var ok = CzTimeEffectSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.Sort);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzTimeEffectSend.TryRead(new byte[3], out _));
    }
}
