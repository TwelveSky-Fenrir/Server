using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

/// <summary>
///     <see cref="CzPartyChatSend.Link" /> is on the wire but dead server-side (see contract remarks) —
///     the contract still decodes it faithfully; it is the Phase C handler's responsibility not to
///     propagate it downstream.
/// </summary>
public class CzPartyChatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(85, CzPartyChatSend.PayloadSize);
        Assert.Equal(61 + ItemLinkInfo.WireSize, CzPartyChatSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.PartyChatSend, CzPartyChatSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 99, Activity = 1, Value = 2, Socket = [3, 4, 5] };

        Span<byte> buffer = new byte[CzPartyChatSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..61], "Group up at the gate");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(buffer[61..], link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);

        var ok = CzPartyChatSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Group up at the gate", packet.Content);
        WireTestKit.AssertDeepEqual(link, packet.Link);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzPartyChatSend.TryRead(new byte[84], out _));
    }
}
