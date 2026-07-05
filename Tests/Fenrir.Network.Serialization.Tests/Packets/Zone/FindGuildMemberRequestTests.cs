using System.Text;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzGuildFindSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, FindGuildMemberRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.FindGuildMember, FindGuildMemberRequest.Opcode);
    }

    [Fact]
    public void TryRead_DecodesFieldsFromManuallyEncodedBuffer()
    {
        Span<byte> buffer = stackalloc byte[FindGuildMemberRequest.PayloadSize];
        buffer.Clear();
        Encoding.Latin1.GetBytes("Loki", buffer[..4]);

        var ok = FindGuildMemberRequest.TryRead(buffer, out var packet);

        Assert.True(ok);
        Assert.Equal("Loki", packet.AvatarName);
    }
}
