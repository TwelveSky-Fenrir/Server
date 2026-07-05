using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcPartyJoinInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, PartyMemberJoinedResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyMemberJoined, PartyMemberJoinedResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<PartyMemberJoinedResponse>(1);

        Span<byte> buffer = new byte[PartyMemberJoinedResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(PartyMemberJoinedResponse.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<PartyMemberJoinedResponse>(11);

        var expected = new byte[PartyMemberJoinedResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[PartyMemberJoinedResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, PartyMemberJoinedResponse value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
    }
}
