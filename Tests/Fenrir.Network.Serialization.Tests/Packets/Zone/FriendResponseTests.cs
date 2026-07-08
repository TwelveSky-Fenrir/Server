using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcFriendAskRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, FriendResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.Friend, FriendResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<FriendResponse>(1);

        Span<byte> buffer = new byte[FriendResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(FriendResponse.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<FriendResponse>(11);

        var expected = new byte[FriendResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[FriendResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, FriendResponse value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
    }
}
