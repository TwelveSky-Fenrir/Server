using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGeneralChatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(85, CzGeneralChatSend.PayloadSize);
        Assert.Equal(61 + ItemLinkInfo.WireSize, CzGeneralChatSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GeneralChatSend, CzGeneralChatSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 501, Activity = 2, Value = 3, Socket = [10, 20, 30] };

        Span<byte> buffer = new byte[CzGeneralChatSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..61], "Hello world");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(buffer[61..], link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);

        var ok = CzGeneralChatSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Hello world", packet.Content);
        WireTestKit.AssertDeepEqual(link, packet.Link);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzGeneralChatSend.TryRead(new byte[84], out _));
    }
}
