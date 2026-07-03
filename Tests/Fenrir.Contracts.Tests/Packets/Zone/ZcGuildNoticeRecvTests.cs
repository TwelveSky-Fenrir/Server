using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGuildNoticeRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(74, ZcGuildNoticeRecv.PayloadSize);
        Assert.Equal(13 + 61, ZcGuildNoticeRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildNoticeRecv, ZcGuildNoticeRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcGuildNoticeRecv { AvatarName = "Odin", Content = "New guild policy in effect." };

        Span<byte> buffer = new byte[ZcGuildNoticeRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcGuildNoticeRecv.PayloadSize, written);

        var golden = new byte[ZcGuildNoticeRecv.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(13, 61), "New guild policy in effect.");

        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var packet = new ZcGuildNoticeRecv { AvatarName = "Thor", Content = "Meeting tonight" };

        Span<byte> buffer = new byte[ZcGuildNoticeRecv.PayloadSize];
        packet.Write(buffer);

        Assert.Equal("Thor", WireTestKit.ReadFixedString(buffer[..13]));
        Assert.Equal("Meeting tonight", WireTestKit.ReadFixedString(buffer[13..]));
    }
}
