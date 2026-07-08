using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Zone.Packets.Zone;

namespace Fenrir.Network.Serialization.Tests.Packets.Zone;

public class CzTeacherCancelSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, MentorCancelRequest.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.MentorCancel, MentorCancelRequest.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = MentorCancelRequest.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new MentorCancelRequest(), packet);
    }
}
