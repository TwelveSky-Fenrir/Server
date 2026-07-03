using System.Text;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzGuildFindSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, CzGuildFindSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildFindSend, CzGuildFindSend.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[CzGuildFindSend.PayloadSize];
        buffer.Clear();
        Encoding.Latin1.GetBytes("Loki", buffer[..4]);

        var ok = CzGuildFindSend.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Loki", packet.AvatarName);
    }
}
