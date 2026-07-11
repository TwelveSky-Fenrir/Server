using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcSecretChatRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(110, WhisperResponse.PayloadSize);
        Assert.Equal(4 + 4 + 13 + 61 + 4 + ItemLinkInfo.WireSize, WhisperResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.Whisper, WhisperResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 4, Activity = 1, Value = 2, Socket = [3, 4, 5] };
        var packet = new WhisperResponse
        {
            Result = 0,
            ZoneNumber = 12,
            AvatarName = "Odin",
            Content = "Meet me at the shrine",
            AuthType = 0,
            Link = link
        };

        Span<byte> buffer = new byte[WhisperResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(WhisperResponse.PayloadSize, written);

        var golden = new byte[WhisperResponse.PayloadSize];
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(0), 0);
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(4), 12);
        WireTestKit.WriteFixedString(golden.AsSpan(8, 13), "Odin");
        WireTestKit.WriteFixedString(golden.AsSpan(21, 61), "Meet me at the shrine");
        BinaryPrimitives.WriteInt32LittleEndian(golden.AsSpan(82), 0);
        var linkWritten = WireTestKit.EncodeItemLinkInfo(golden.AsSpan(86), link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);
        Assert.Equal(golden, buffer.ToArray());
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode_TargetOfflineResult()
    {
        var packet = new WhisperResponse
        {
            Result = 1,
            ZoneNumber = 0,
            AvatarName = "Ghost",
            Content = "",
            AuthType = 0,
            Link = new ItemLinkInfo { Index = 0, Activity = 0, Value = 0, Socket = [0, 0, 0] }
        };

        Span<byte> buffer = new byte[WhisperResponse.PayloadSize];
        packet.Write(buffer);

        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buffer[4..]));
        Assert.Equal("Ghost", WireTestKit.ReadFixedString(buffer.Slice(8, 13)));
        Assert.Equal(0, BinaryPrimitives.ReadInt32LittleEndian(buffer[82..]));

        Assert.True(ItemLinkInfo.TryRead(buffer.Slice(86, ItemLinkInfo.WireSize), out var linkBack));
        WireTestKit.AssertDeepEqual(packet.Link, linkBack);
    }

    [Fact]
    public void Write_RoundTrips_ViaManualDecode_SystemMessageResult()
    {
        var link = new ItemLinkInfo { Index = 0, Activity = 0, Value = 0, Socket = [0, 0, 0] };
        var packet = new WhisperResponse
        {
            Result = 3,
            ZoneNumber = 0,
            AvatarName = "-Server-",
            Content = "You have been AFK too long.",
            AuthType = 1,
            Link = link
        };

        Span<byte> buffer = new byte[WhisperResponse.PayloadSize];
        packet.Write(buffer);

        Assert.Equal(3, BinaryPrimitives.ReadInt32LittleEndian(buffer));
        Assert.Equal("-Server-", WireTestKit.ReadFixedString(buffer.Slice(8, 13)));
        Assert.Equal("You have been AFK too long.", WireTestKit.ReadFixedString(buffer.Slice(21, 61)));
        Assert.Equal(1, BinaryPrimitives.ReadInt32LittleEndian(buffer[82..]));

        Assert.True(ItemLinkInfo.TryRead(buffer.Slice(86, ItemLinkInfo.WireSize), out var linkBack));
        WireTestKit.AssertDeepEqual(link, linkBack);
    }
}
