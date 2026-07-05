using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTimeEffectSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, PlaytimeBuffRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PlaytimeBuff, PlaytimeBuffRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[PlaytimeBuffRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 3);

        var ok = PlaytimeBuffRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.Sort);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(PlaytimeBuffRequest.TryRead(new byte[3], out _));
    }
}
