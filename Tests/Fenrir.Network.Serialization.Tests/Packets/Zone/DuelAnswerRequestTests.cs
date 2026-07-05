using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzDuelAnswerSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, DuelAnswerRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.DuelAnswer, DuelAnswerRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[DuelAnswerRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(0), 1006);

        var ok = DuelAnswerRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal(1006, packet.Answer);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(DuelAnswerRequest.TryRead(new byte[3], out _));
    }
}
