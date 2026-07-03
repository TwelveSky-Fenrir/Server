using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzPartyAnswerSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, PartyAnswerRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PartyAnswer, PartyAnswerRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[PartyAnswerRequest.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(0), 1006);

        var ok = PartyAnswerRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal(1006, packet.Answer);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(PartyAnswerRequest.TryRead(new byte[3], out _));
    }
}
