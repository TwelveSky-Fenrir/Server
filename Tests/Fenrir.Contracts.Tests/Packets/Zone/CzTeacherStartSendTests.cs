using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTeacherStartSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, CzTeacherStartSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TeacherStartSend, CzTeacherStartSend.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = CzTeacherStartSend.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new CzTeacherStartSend(), packet);
    }
}
