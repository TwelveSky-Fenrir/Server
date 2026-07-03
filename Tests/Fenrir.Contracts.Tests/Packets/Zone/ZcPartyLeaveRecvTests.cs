using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcPartyLeaveRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, ZcPartyLeaveRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyLeaveRecv, ZcPartyLeaveRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcPartyLeaveRecv>(1);

        Span<byte> buffer = new byte[ZcPartyLeaveRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcPartyLeaveRecv.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcPartyLeaveRecv>(11);

        var expected = new byte[ZcPartyLeaveRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcPartyLeaveRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcPartyLeaveRecv value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
    }
}
