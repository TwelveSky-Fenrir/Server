using Fenrir.Contracts.Packets.Zone;

namespace Fenrir.Contracts.Tests.Packets.Zone;

public class CzTeacherStateSendTests
{
    [Fact]
    public void PayloadSize_MatchesContract()
    {
        Assert.Equal(0, CzTeacherStateSend.PayloadSize);
        Assert.Equal(Opcodes.Zone.Incoming.TeacherStateSend, CzTeacherStateSend.Opcode);
    }

    [Fact]
    public void TryRead_EmptyPayload_Succeeds()
    {
        var ok = CzTeacherStateSend.TryRead(Array.Empty<byte>(), out var packet);

        Assert.True(ok);
        Assert.Equal(new CzTeacherStateSend(), packet);
    }
}
