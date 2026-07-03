using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzWorldChatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(61, CzWorldChatSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.WorldChatSend, CzWorldChatSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        Span<byte> buffer = new byte[CzWorldChatSend.PayloadSize];
        WireTestKit.WriteFixedString(buffer, "Anyone selling a mount?");

        var ok = CzWorldChatSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Anyone selling a mount?", packet.Content);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(CzWorldChatSend.TryRead(new byte[60], out _));
    }
}
