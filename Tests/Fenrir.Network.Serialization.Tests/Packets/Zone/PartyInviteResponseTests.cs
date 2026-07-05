using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcPartyAskRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, PartyInviteResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyInvite, PartyInviteResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<PartyInviteResponse>(1);

        Span<byte> buffer = new byte[PartyInviteResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(PartyInviteResponse.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<PartyInviteResponse>(11);

        var expected = new byte[PartyInviteResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[PartyInviteResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, PartyInviteResponse value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
    }
}
