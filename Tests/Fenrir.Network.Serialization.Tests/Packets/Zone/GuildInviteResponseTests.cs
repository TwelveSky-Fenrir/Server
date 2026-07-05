using System.Text;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcGuildAskRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, GuildInviteResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildInvite, GuildInviteResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new GuildInviteResponse { AvatarName = "Odin" };

        Span<byte> buffer = stackalloc byte[GuildInviteResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(GuildInviteResponse.PayloadSize, written);

        var golden = new byte[13];
        Encoding.Latin1.GetBytes("Odin", golden.AsSpan(0, 4));
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
