using Fenrir.Application.Login.Domain.Avatars;

namespace Fenrir.Application.Login.Tests.Avatars;

public class FourthFactionGateTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BlocksCreation_TribeZeroToTwo_NeverBlocks(byte tribe)
    {
        Assert.False(FourthFactionGate.BlocksCreation(tribe));
    }

    [Fact]
    public void BlocksCreation_TribeThree_AlwaysBlocksUnconditionally()
    {
        Assert.True(FourthFactionGate.BlocksCreation(FourthFactionGate.FourthFactionTribe));
    }

    [Fact]
    public void FourthFactionTribe_IsDerivedFromTheSharedTribeSlotCount_NotASecondIndependentLiteral()
    {
        Assert.Equal(TribeDominanceGate.TribeSlotCount - 1, FourthFactionGate.FourthFactionTribe);
        Assert.Equal(3, FourthFactionGate.FourthFactionTribe);
    }
}
