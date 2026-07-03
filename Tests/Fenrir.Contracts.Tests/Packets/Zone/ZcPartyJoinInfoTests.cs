using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcPartyJoinInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, ZcPartyJoinInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyJoinInfo, ZcPartyJoinInfo.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcPartyJoinInfo>(1);

        Span<byte> buffer = new byte[ZcPartyJoinInfo.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcPartyJoinInfo.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcPartyJoinInfo>(11);

        var expected = new byte[ZcPartyJoinInfo.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcPartyJoinInfo.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcPartyJoinInfo value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
    }
}
