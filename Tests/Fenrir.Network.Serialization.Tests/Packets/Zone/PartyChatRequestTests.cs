using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

// Link rides the wire but is dead server-side; still decoded faithfully here.
public class CzPartyChatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(85, PartyChatRequest.PayloadSize);
        Assert.Equal(61 + ItemLinkInfo.WireSize, PartyChatRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PartyChat, PartyChatRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 99, Activity = 1, Value = 2, Socket = [3, 4, 5] };

        Span<byte> buffer = new byte[PartyChatRequest.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..61], "Group up at the gate");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(buffer[61..], link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);

        var ok = PartyChatRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Group up at the gate", packet.Content);
        WireTestKit.AssertDeepEqual(link, packet.Link);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(PartyChatRequest.TryRead(new byte[84], out _));
    }
}
