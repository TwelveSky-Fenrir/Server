using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzChangeAutoInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(8, CzChangeAutoInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.ChangeAutoInfo, CzChangeAutoInfo.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzChangeAutoInfo.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 3);
        BinaryPrimitives.WriteInt32LittleEndian(buffer[4..], 4);

        var ok = CzChangeAutoInfo.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(3, packet.Value01);
        Assert.Equal(4, packet.Value02);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzChangeAutoInfo.TryRead(new byte[7], out _));
    }
}
