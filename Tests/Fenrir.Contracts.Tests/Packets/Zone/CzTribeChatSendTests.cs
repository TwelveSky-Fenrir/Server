using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTribeChatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(85, CzTribeChatSend.PayloadSize);
        Assert.Equal(61 + ItemLinkInfo.WireSize, CzTribeChatSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeChatSend, CzTribeChatSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 8, Activity = 6, Value = 1, Socket = [0, 0, 1] };

        Span<byte> buffer = new byte[CzTribeChatSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..61], "Defend the outpost");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(buffer[61..], link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);

        var ok = CzTribeChatSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Defend the outpost", packet.Content);
        WireTestKit.AssertDeepEqual(link, packet.Link);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzTribeChatSend.TryRead(new byte[84], out _));
    }
}
