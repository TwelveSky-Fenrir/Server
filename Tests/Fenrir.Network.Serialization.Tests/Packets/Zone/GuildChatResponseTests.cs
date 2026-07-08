using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcGuildChatRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(98, GuildChatResponse.PayloadSize);
        Assert.Equal(13 + 61 + ItemLinkInfo.WireSize, GuildChatResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildChat, GuildChatResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 21, Activity = 3, Value = 4, Socket = [5, 6, 7] };
        var packet = new GuildChatResponse { AvatarName = "Odin", Content = "gz on the drop", Link = link };

        Span<byte> buffer = new byte[GuildChatResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(GuildChatResponse.PayloadSize, written);

        var golden = new byte[GuildChatResponse.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(13, 61), "gz on the drop");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(golden.AsSpan(74), link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);
        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var link = new ItemLinkInfo { Index = 2, Activity = 2, Value = 2, Socket = [2, 2, 2] };
        var packet = new GuildChatResponse { AvatarName = "Thor", Content = "guild chat echo", Link = link };

        Span<byte> buffer = new byte[GuildChatResponse.PayloadSize];
        packet.Write(buffer);

        Assert.Equal("Thor", WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
        Assert.Equal("guild chat echo", WireTestKit.ReadFixedString(buffer.Slice(13, 61)));

        Assert.True(ItemLinkInfo.TryRead(buffer.Slice(74, ItemLinkInfo.WireSize), out var linkBack));
        WireTestKit.AssertDeepEqual(link, linkBack);
    }
}
