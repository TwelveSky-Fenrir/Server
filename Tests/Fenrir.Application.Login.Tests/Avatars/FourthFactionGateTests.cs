using Fenrir.Application.Login.Domain.Avatars;

namespace Fenrir.Application.Login.Tests.Avatars;

// op17's fourth-faction (Tribe value 3) creation exclusion -- LNW33-gated in legacy, always active in the
// sole production-shipped build (ReleaseEU33). Réf. C++ : Server/ts25login/S04_MyWork02.cpp:635-646.
public class FourthFactionGateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BlocksCreation_TribeZeroToTwo_NeverBlocksRegardlessOfToggle(byte tribe)
    {
        Assert.False(FourthFactionGate.BlocksCreation(tribe, false));
        Assert.False(FourthFactionGate.BlocksCreation(tribe, true));
    }

    [Fact]
    public void BlocksCreation_TribeThree_ToggleInDefaultDisabledState_Blocks()
    {
        Assert.True(FourthFactionGate.BlocksCreation(FourthFactionGate.FourthFactionTribe,
            false));
    }

    [Fact]
    public void BlocksCreation_TribeThree_ToggleOperatorEnabled_DoesNotBlock()
    {
        Assert.False(FourthFactionGate.BlocksCreation(FourthFactionGate.FourthFactionTribe,
            true));
    }

    [Fact]
    public void FourthFactionTribe_IsDerivedFromTheSharedTribeSlotCount_NotASecondIndependentLiteral()
    {
        // Guards against the two gates drifting out of lockstep -- both ultimately come from the same
        // Server/Header/Protocol/DEFINE.h:309 four-tribe-slot constant.
        Assert.Equal(TribeDominanceGate.TribeSlotCount - 1, FourthFactionGate.FourthFactionTribe);
        Assert.Equal(3, FourthFactionGate.FourthFactionTribe);
    }
}
