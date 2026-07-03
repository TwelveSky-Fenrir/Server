using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTribeChatRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(98, ZcTribeChatRecv.PayloadSize);
        Assert.Equal(13 + 61 + ItemLinkInfo.WireSize, ZcTribeChatRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.TribeChatRecv, ZcTribeChatRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 6, Activity = 5, Value = 4, Socket = [3, 2, 1] };
        var packet = new ZcTribeChatRecv { AvatarName = "Odin", Content = "For the tribe!", Link = link };

        Span<byte> buffer = new byte[ZcTribeChatRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcTribeChatRecv.PayloadSize, written);

        var golden = new byte[ZcTribeChatRecv.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(13, 61), "For the tribe!");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(golden.AsSpan(74), link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);
        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var link = new ItemLinkInfo { Index = 9, Activity = 9, Value = 9, Socket = [9, 9, 9] };
        var packet = new ZcTribeChatRecv { AvatarName = "Freya", Content = "Ally spotted", Link = link };

        Span<byte> buffer = new byte[ZcTribeChatRecv.PayloadSize];
        packet.Write(buffer);

        Assert.Equal("Freya", WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
        Assert.Equal("Ally spotted", WireTestKit.ReadFixedString(buffer.Slice(13, 61)));

        Assert.True(ItemLinkInfo.TryRead(buffer.Slice(74, ItemLinkInfo.WireSize), out var linkBack));
        WireTestKit.AssertDeepEqual(link, linkBack);
    }
}
