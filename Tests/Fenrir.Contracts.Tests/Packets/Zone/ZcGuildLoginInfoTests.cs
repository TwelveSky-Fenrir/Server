using System.Text;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGuildLoginInfoTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, ZcGuildLoginInfo.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildLoginInfo, ZcGuildLoginInfo.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcGuildLoginInfo { AvatarName = "Freya" };

        Span<byte> buffer = stackalloc byte[ZcGuildLoginInfo.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcGuildLoginInfo.PayloadSize, written);

        var golden = new byte[13];
        Encoding.Latin1.GetBytes("Freya", golden.AsSpan(0, 5));
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
