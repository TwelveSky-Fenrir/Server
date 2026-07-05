using System.Buffers.Binary;
using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcGuildFindRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(4, FindGuildMemberResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.FindGuildMember, FindGuildMemberResponse.Opcode);
    }

    [Fact]
    public void Write_ProducesGoldenBytes()
    {
        var packet = new FindGuildMemberResponse { Result = 17 };

        Span<byte> buffer = stackalloc byte[FindGuildMemberResponse.PayloadSize];
        var written = packet.Write(buffer);

        Assert.Equal(FindGuildMemberResponse.PayloadSize, written);
        Assert.Equal(17, BinaryPrimitives.ReadInt32LittleEndian(buffer));
    }
}
