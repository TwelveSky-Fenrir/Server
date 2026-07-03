using System.Text;
using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcGuildAskRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(13, ZcGuildAskRecv.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.GuildAskRecv, ZcGuildAskRecv.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new ZcGuildAskRecv { AvatarName = "Odin" };

        Span<byte> buffer = stackalloc byte[ZcGuildAskRecv.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(ZcGuildAskRecv.PayloadSize, written);

        var golden = new byte[13];
        Encoding.Latin1.GetBytes("Odin", golden.AsSpan(0, 4));
        Assert.True(golden.AsSpan().SequenceEqual(buffer));
    }
}
