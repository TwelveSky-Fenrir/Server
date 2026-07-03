using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzPartyAnswerSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, CzPartyAnswerSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PartyAnswerSend, CzPartyAnswerSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[CzPartyAnswerSend.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(0), 1006);

        var ok = CzPartyAnswerSend.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal(1006, packet.Answer);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzPartyAnswerSend.TryRead(new byte[3], out _));
    }
}
