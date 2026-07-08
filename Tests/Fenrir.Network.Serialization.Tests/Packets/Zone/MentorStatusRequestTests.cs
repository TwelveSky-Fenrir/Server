using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzTeacherStateSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, MentorStatusRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.MentorStatus, MentorStatusRequest.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = MentorStatusRequest.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new MentorStatusRequest(), packet);
    }
}
