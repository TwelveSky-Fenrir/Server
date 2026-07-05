using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcGeneralShoutRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(98, ShoutResponse.PayloadSize);
        Assert.Equal(13 + 61 + ItemLinkInfo.WireSize, ShoutResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.Shout, ShoutResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 3, Activity = 1, Value = 2, Socket = [7, 8, 9] };
        var packet = new ShoutResponse { AvatarName = "Odin", Content = "SHOUT!", Link = link };

        Span<byte> buffer = new byte[ShoutResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ShoutResponse.PayloadSize, written);

        var golden = new byte[ShoutResponse.PayloadSize];
        WireTestKit.WriteFixedString(golden.AsSpan(0, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(13, 61), "SHOUT!");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(golden.AsSpan(74), link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);
        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode()
    {
        var link = new ItemLinkInfo { Index = 0, Activity = 0, Value = 0, Socket = [0, 0, 0] };
        var packet = new ShoutResponse { AvatarName = "Freya", Content = "Everyone hears this", Link = link };

        Span<byte> buffer = new byte[ShoutResponse.PayloadSize];
        packet.Write(buffer);

        Assert.Equal("Freya", WireTestKit.ReadFixedString(buffer.Slice(0, 13)));
        Assert.Equal("Everyone hears this", WireTestKit.ReadFixedString(buffer.Slice(13, 61)));

        Assert.True(ItemLinkInfo.TryRead(buffer.Slice(74, ItemLinkInfo.WireSize), out var linkBack));
        WireTestKit.AssertDeepEqual(link, linkBack);
    }
}
