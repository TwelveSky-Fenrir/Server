using System.Text;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzGuildAskSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, GuildInviteRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.GuildInvite, GuildInviteRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[GuildInviteRequest.PayloadSize];
        buffer.Clear();
        Encoding.Latin1.GetBytes("Thor", buffer[..4]);

        var ok = GuildInviteRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Thor", packet.AvatarName);
    }
}
