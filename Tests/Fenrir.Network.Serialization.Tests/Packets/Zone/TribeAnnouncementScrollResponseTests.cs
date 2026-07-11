using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTribeNotifyRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(78, TribeAnnouncementScrollResponse.PayloadSize);
        Assert.Equal(4 + 13 + 61, TribeAnnouncementScrollResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeAnnouncementScroll, TribeAnnouncementScrollResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new TribeAnnouncementScrollResponse
            { TribeRole = 3, AvatarName = "Odin", Content = "Scroll used!" };

        Span<byte> buffer = new byte[TribeAnnouncementScrollResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(TribeAnnouncementScrollResponse.PayloadSize, written);

        var golden = new byte[TribeAnnouncementScrollResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 3);
        WireTestKit.WriteFixedString(golden.AsSpan(4, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(17, 61), "Scroll used!");

        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var packet = new TribeAnnouncementScrollResponse
            { TribeRole = 4, AvatarName = "Freya", Content = "Tribe number 4" };

        Span<byte> buffer = new byte[TribeAnnouncementScrollResponse.PayloadSize];
        packet.Write(buffer);

        Assert.Equal(4, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal("Freya", WireTestKit.ReadFixedString(buffer.Slice(4, 13)));
        Assert.Equal("Tribe number 4", WireTestKit.ReadFixedString(buffer[17..]));
    }
}
