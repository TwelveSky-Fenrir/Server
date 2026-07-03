using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTeacherEndSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, CzTeacherEndSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TeacherEndSend, CzTeacherEndSend.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = CzTeacherEndSend.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new CzTeacherEndSend(), packet);
    }
}
