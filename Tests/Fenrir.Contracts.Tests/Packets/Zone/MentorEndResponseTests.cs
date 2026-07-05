using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class ZcTeacherEndRecvTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, MentorEndResponse.PayloadSize);
        Assert.Equal(Opcodes.Zone.Outgoing.MentorEnd, MentorEndResponse.Opcode);
    }

    [Fact]
    public void Write_EmptyPayload_ReturnsZero()
    {
        var packet = new MentorEndResponse();

        Span<byte> buffer = [];
        var written = packet.Write(buffer);

        Assert.Equal(0, written);
    }
}
