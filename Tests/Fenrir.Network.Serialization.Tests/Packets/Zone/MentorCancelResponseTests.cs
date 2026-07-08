using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class ZcTeacherCancelRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, MentorCancelResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.MentorCancel, MentorCancelResponse.Opcode);
    }

    [Fact]
    public void Write_EmptyPayload_ReturnsZero()
    {
        var packet = new MentorCancelResponse();

        Span<byte> buffer = [];
        var written = packet.Write(buffer);

        Assert.Equal(0, written);
    }
}
