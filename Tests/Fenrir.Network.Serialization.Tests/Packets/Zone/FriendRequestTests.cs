using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzFriendAskSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, FriendRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.Friend, FriendRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var golden = new byte[FriendRequest.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Nm0A");

        var ok = FriendRequest.TryRead(golden, out var packet);

        Assert.True(ok);
        Assert.Equal("Nm0A", packet.AvatarName);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(FriendRequest.TryRead(new byte[12], out _));
    }
}
