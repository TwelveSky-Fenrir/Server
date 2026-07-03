using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzRankBuffSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzRankBuffSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.RankBuffSend, CzRankBuffSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzRankBuffSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 5);

        var ok = CzRankBuffSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(5, packet.Sort);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzRankBuffSend.TryRead(new byte[3], out _));
    }
}
