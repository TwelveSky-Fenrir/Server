using System.Buffers.Binary;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzDuelAskSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(17, DuelChallengeRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.DuelChallenge, DuelChallengeRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[DuelChallengeRequest.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Nm0A");
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(13), 1021);

        var ok = DuelChallengeRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal("Nm0A", packet.AvatarName);
        Assert.Equal(1021, packet.Sort);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(DuelChallengeRequest.TryRead(new byte[16], out _));
    }
}
