using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzSecretChatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(98, CzSecretChatSend.PayloadSize);
        Assert.Equal(13 + 61 + ItemLinkInfo.WireSize, CzSecretChatSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.SecretChatSend, CzSecretChatSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        var link = new ItemLinkInfo { Index = 7, Activity = 1, Value = 0, Socket = [1, 2, 3] };

        Span<byte> buffer = new byte[CzSecretChatSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer[..13], "Freya");
        WireTestKit.WriteFixedString(buffer.Slice(13, 61), "Hi there, got a minute?");
        var linkWritten = WireTestKit.EncodeItemLinkInfo(buffer[74..], link);

        Assert.Equal(ItemLinkInfo.WireSize, linkWritten);

        var ok = CzSecretChatSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Freya", packet.AvatarName);
        Assert.Equal("Hi there, got a minute?", packet.Content);
        WireTestKit.AssertDeepEqual(link, packet.Link);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzSecretChatSend.TryRead(new byte[97], out _));
    }
}
