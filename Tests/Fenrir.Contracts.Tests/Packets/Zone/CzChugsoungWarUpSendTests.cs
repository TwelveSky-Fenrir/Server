using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzChugsoungWarUpSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(12, CzChugsoungWarUpSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ChugsoungWarUpSend, CzChugsoungWarUpSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzChugsoungWarUpSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 7);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 2);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[8..], 4);

        var ok = CzChugsoungWarUpSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(7, packet.Index);
        Assert.Equal(2, packet.Value01);
        Assert.Equal(4, packet.Value02);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzChugsoungWarUpSend.TryRead(new byte[11], out _));
    }
}
