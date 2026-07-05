using System.Text;
using Fenrir.Contracts;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcGuildLoginInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, GuildMemberConnectedResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildMemberConnected, GuildMemberConnectedResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new GuildMemberConnectedResponse { AvatarName = "Freya" };

        Span<byte> buffer = stackalloc byte[GuildMemberConnectedResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(GuildMemberConnectedResponse.PayloadSize, written);

        var golden = new byte[13];
        Encoding.Latin1.GetBytes("Freya", golden.AsSpan(0, 5));
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
