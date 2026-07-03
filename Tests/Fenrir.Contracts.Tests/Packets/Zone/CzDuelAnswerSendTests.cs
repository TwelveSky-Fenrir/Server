using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzDuelAnswerSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzDuelAnswerSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.DuelAnswerSend, CzDuelAnswerSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[CzDuelAnswerSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(0), 1006);

        var ok = CzDuelAnswerSend.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal(1006, packet.Answer);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzDuelAnswerSend.TryRead(new byte[3], out _));
    }
}
