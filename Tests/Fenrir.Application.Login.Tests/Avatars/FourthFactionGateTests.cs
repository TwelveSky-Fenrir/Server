using Fenrir.Application.Login.Domain.Avatars;

namespace Fenrir.Application.Login.Tests.Avatars;

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
        Assert.Equal(TribeDominanceGate.TribeSlotCount - 1, FourthFactionGate.FourthFactionTribe);
        Assert.Equal(3, FourthFactionGate.FourthFactionTribe);
    }
}
