using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzTribeChatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(85, TribeChatRequest.PayloadSize);
        Assert.Equal(61 + ItemLinkInfo.WireSize, TribeChatRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TribeChat, TribeChatRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 8, Activity = 6, Value = 1, Socket = [0, 0, 1] };

        Span<byte> buffer = new byte[TribeChatRequest.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..61], "Defend the outpost");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(buffer[61..], link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);

        var ok = TribeChatRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Defend the outpost", packet.Content);
        WireTestKit.AssertDeepEqual(link, packet.Link);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(TribeChatRequest.TryRead(new byte[84], out _));
    }
}
