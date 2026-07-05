using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzWorldChatSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(61, WorldChatRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.WorldChat, WorldChatRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesGoldenBytes()
    {
        Span<byte> buffer = new byte[WorldChatRequest.PayloadSize];
        WireTestKit.WriteFixedString(buffer, "Anyone selling a mount?");

        var ok = WorldChatRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Anyone selling a mount?", packet.Content);
    }

    [Fact]
    public void TryRead_TooShort_Fails()
    {
        Assert.False(WorldChatRequest.TryRead(new byte[60], out _));
    }
}
