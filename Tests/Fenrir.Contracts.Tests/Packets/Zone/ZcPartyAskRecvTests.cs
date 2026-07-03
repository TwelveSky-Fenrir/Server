using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcPartyAskRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, ZcPartyAskRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.PartyAskRecv, ZcPartyAskRecv.Opcode);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualOffsetRead()
    {
        var packet = WireTestKit.CreatePopulated<ZcPartyAskRecv>(1);

        Span<byte> buffer = new byte[ZcPartyAskRecv.PayloadSize];
        var written = packet.Write(buffer);
        Assert.Equal(ZcPartyAskRecv.PayloadSize, written);

        Assert.Equal(packet.AvatarName, WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = WireTestKit.CreatePopulated<ZcPartyAskRecv>(11);

        var expected = new byte[ZcPartyAskRecv.PayloadSize];
        EncodeGolden(expected, packet);

        Span<byte> actual = new byte[ZcPartyAskRecv.PayloadSize];
        packet.Write(actual);

        Assert.Equal(expected, actual);
    }

    private static void EncodeGolden(Span<byte> destination, ZcPartyAskRecv value)
    {
        WireTestKit.WriteFixedString(destination.Slice(0, 13), value.AvatarName);
    }
}
