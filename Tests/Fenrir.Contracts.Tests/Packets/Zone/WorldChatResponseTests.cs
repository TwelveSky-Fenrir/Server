using System.Buffers.Binary;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcWorldChatRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(78, WorldChatResponse.PayloadSize);
        Assert.Equal(4 + 13 + 61, WorldChatResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.WorldChat, WorldChatResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new WorldChatResponse { TribeRole = 2, AvatarName = "Odin", Content = "Selling rare mount" };

        Span<byte> buffer = new byte[WorldChatResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(WorldChatResponse.PayloadSize, written);

        var golden = new byte[WorldChatResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden, 2);
        WireTestKit.WriteFixedString(golden.AsSpan(4, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(17, 61), "Selling rare mount");

        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var packet = new WorldChatResponse { TribeRole = 0, AvatarName = "Freya", Content = "No tribe" };

        Span<byte> buffer = new byte[WorldChatResponse.PayloadSize];
        packet.Write(buffer);

        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal("Freya", WireTestKit.ReadFixedString(buffer.Slice(4, 13)));
        Assert.Equal("No tribe", WireTestKit.ReadFixedString(buffer[17..]));
    }

    [Fact]
    public void DistinctRecord_FromZcTribeNotifyRecv_DespiteIdenticalLayout()
    {
        Assert.NotEqual(TribeAnnouncementScrollResponse.Opcode, WorldChatResponse.Opcode);
        Assert.Equal(TribeAnnouncementScrollResponse.PayloadSize, WorldChatResponse.PayloadSize);
    }
}
