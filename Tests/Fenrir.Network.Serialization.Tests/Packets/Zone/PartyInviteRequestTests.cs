using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzPartyAskSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, PartyInviteRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PartyInvite, PartyInviteRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[PartyInviteRequest.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Nm0A");

        var ok = PartyInviteRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal("Nm0A", packet.AvatarName);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(PartyInviteRequest.TryRead(new byte[12], out _));
    }
}
