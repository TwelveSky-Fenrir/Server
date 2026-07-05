using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcPartyExileRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, PartyKickResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyKick, PartyKickResponse.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<PartyKickResponse>(1);

        Span<byte> buffer = new byte[PartyKickResponse.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(PartyKickResponse.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<PartyKickResponse>(11);

        var expected = new byte[PartyKickResponse.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[PartyKickResponse.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, PartyKickResponse value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
    }
}
