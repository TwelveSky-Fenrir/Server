using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzPartyExileSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, PartyKickRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PartyKick, PartyKickRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[PartyKickRequest.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Nm0A");

        var ok = PartyKickRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal("Nm0A", packet.AvatarName);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(PartyKickRequest.TryRead(new byte[12], out _));
    }
}
