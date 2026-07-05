using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzRankBuffSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, RankBuffRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.RankBuff, RankBuffRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[RankBuffRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(buffer, 5);

        var ok = RankBuffRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal(5, packet.Sort);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(RankBuffRequest.TryRead(new byte[3], out _));
    }
}
