using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGuildChatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(85, CzGuildChatSend.PayloadSize);
        Assert.Equal(61 + ItemLinkInfo.WireSize, CzGuildChatSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildChatSend, CzGuildChatSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 4, Activity = 4, Value = 4, Socket = [4, 4, 4] };

        Span<byte> buffer = new byte[CzGuildChatSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..61], "Raid at 9pm server time");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(buffer[61..], link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);

        var ok = CzGuildChatSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Raid at 9pm server time", packet.Content);
        WireTestKit.AssertDeepEqual(link, packet.Link);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzGuildChatSend.TryRead(new byte[84], out _));
    }
}
