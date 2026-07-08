using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcGeneralChatRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(98, LocalChatResponse.PayloadSize);
        Assert.Equal(13 + 61 + ItemLinkInfo.WireSize, LocalChatResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.LocalChat, LocalChatResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 55, Activity = 2, Value = 9, Socket = [1, 2, 3] };
        var packet = new LocalChatResponse { AvatarName = "Odin", Content = "Hello world", Link = link };

        Span<byte> buffer = new byte[LocalChatResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(LocalChatResponse.PayloadSize, written);

        var golden = new byte[LocalChatResponse.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(13, 61), "Hello world");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(golden.AsSpan(74), link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);
        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var link = new ItemLinkInfo { Index = 1, Activity = 0, Value = 0, Socket = [0, 0, 0] };
        var packet = new LocalChatResponse { AvatarName = "Thor", Content = "gg", Link = link };

        Span<byte> buffer = new byte[LocalChatResponse.PayloadSize];
        packet.Write(buffer);

        Assert.Equal("Thor", WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
        Assert.Equal("gg", WireTestKit.ReadFixedString(buffer.Slice(13, 61)));

        Assert.True(ItemLinkInfo.TryRead(buffer.Slice(74, ItemLinkInfo.WireSize), out var linkBack));
        WireTestKit.AssertDeepEqual(link, linkBack);
    }
}
