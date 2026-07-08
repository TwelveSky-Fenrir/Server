using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzGeneralChatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(85, LocalChatRequest.PayloadSize);
        Assert.Equal(61 + ItemLinkInfo.WireSize, LocalChatRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.LocalChat, LocalChatRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 501, Activity = 2, Value = 3, Socket = [10, 20, 30] };

        Span<byte> buffer = new byte[LocalChatRequest.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..61], "Hello world");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(buffer[61..], link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);

        var ok = LocalChatRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Hello world", packet.Content);
        WireTestKit.AssertDeepEqual(link, packet.Link);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(LocalChatRequest.TryRead(new byte[84], out _));
    }
}
