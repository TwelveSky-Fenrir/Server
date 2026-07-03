using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGuildChatRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(98, ZcGuildChatRecv.PayloadSize);
        Assert.Equal(13 + 61 + ItemLinkInfo.WireSize, ZcGuildChatRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildChatRecv, ZcGuildChatRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 21, Activity = 3, Value = 4, Socket = [5, 6, 7] };
        var packet = new ZcGuildChatRecv { AvatarName = "Odin", Content = "gz on the drop", Link = link };

        Span<byte> buffer = new byte[ZcGuildChatRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcGuildChatRecv.PayloadSize, written);

        var golden = new byte[ZcGuildChatRecv.PayloadSize];
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
        var packet = new ZcGuildChatRecv { AvatarName = "Thor", Content = "guild chat echo", Link = link };

        Span<byte> buffer = new byte[ZcGuildChatRecv.PayloadSize];
        packet.Write(buffer);

        Assert.Equal("Thor", WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
        Assert.Equal("guild chat echo", WireTestKit.ReadFixedString(buffer.Slice(13, 61)));

        Assert.True(ItemLinkInfo.TryRead(buffer.Slice(74, ItemLinkInfo.WireSize), out var linkBack));
        WireTestKit.AssertDeepEqual(link, linkBack);
    }
}
