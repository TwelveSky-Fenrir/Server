using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzGuildChatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(85, GuildChatRequest.PayloadSize);
        Assert.Equal(61 + ItemLinkInfo.WireSize, GuildChatRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildChat, GuildChatRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 4, Activity = 4, Value = 4, Socket = [4, 4, 4] };

        Span<byte> buffer = new byte[GuildChatRequest.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..61], "Raid at 9pm server time");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(buffer[61..], link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);

        var ok = GuildChatRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Raid at 9pm server time", packet.Content);
        WireTestKit.AssertDeepEqual(link, packet.Link);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(GuildChatRequest.TryRead(new byte[84], out _));
    }
}
