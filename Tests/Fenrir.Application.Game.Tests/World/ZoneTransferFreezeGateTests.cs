using Fenrir.Application.Game.Domain.World;

namespace Fenrir.Application.Game.Tests.World;

public class ZoneTransferFreezeGateTests
{
    private const byte AdmittedOpcode = 21;
    private const byte OtherOpcode = 15;

    [Fact]
    public void NotMovingZone_NeverWithholdsAnyOpcode()
    {
        Assert.False(ZoneTransferFreezeGate.ShouldWithhold(false, OtherOpcode, AdmittedOpcode));
        Assert.False(ZoneTransferFreezeGate.ShouldWithhold(false, AdmittedOpcode, AdmittedOpcode));
    }

    [Fact]
    public void MovingZone_WithholdsEveryOpcodeExceptTheAdmittedOne()
    {
        Assert.True(ZoneTransferFreezeGate.ShouldWithhold(true, OtherOpcode, AdmittedOpcode));
    }

    [Fact]
    public void MovingZone_NeverWithholdsTheAdmittedOpcode()
    {
        Assert.False(ZoneTransferFreezeGate.ShouldWithhold(true, AdmittedOpcode, AdmittedOpcode));
    }
}
