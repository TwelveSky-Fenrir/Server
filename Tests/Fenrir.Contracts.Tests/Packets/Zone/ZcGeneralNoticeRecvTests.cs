using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGeneralNoticeRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(61, ZcGeneralNoticeRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GeneralNoticeRecv, ZcGeneralNoticeRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcGeneralNoticeRecv { Content = "Rare item crafted!" };

        Span<byte> buffer = new byte[ZcGeneralNoticeRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcGeneralNoticeRecv.PayloadSize, written);

        var golden = new byte[ZcGeneralNoticeRecv.PayloadSize];
        WireTestKit.WriteFixedString(golden, "Rare item crafted!");

        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var packet = new ZcGeneralNoticeRecv { Content = "System notice" };

        Span<byte> buffer = new byte[ZcGeneralNoticeRecv.PayloadSize];
        packet.Write(buffer);

        Assert.Equal("System notice", WireTestKit.ReadFixedString(buffer));
    }
}
