using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGeneralShoutSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(85, CzGeneralShoutSend.PayloadSize);
        Assert.Equal(61 + ItemLinkInfo.WireSize, CzGeneralShoutSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GeneralShoutSend, CzGeneralShoutSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 12, Activity = 0, Value = 5, Socket = [1, 0, 0] };

        Span<byte> buffer = new byte[CzGeneralShoutSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..61], "SHOUTING TO EVERYONE");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(buffer[61..], link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);

        var ok = CzGeneralShoutSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("SHOUTING TO EVERYONE", packet.Content);
        WireTestKit.AssertDeepEqual(link, packet.Link);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzGeneralShoutSend.TryRead(new byte[84], out _));
    }
}
