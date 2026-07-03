using System.Text;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGuildAskSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, CzGuildAskSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildAskSend, CzGuildAskSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzGuildAskSend.PayloadSize];
        buffer.Clear();
        Encoding.Latin1.GetBytes("Thor", buffer[..4]);

        var ok = CzGuildAskSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Thor", packet.AvatarName);
    }
}
