using Fenrir.Application.Game.Progression;

namespace Fenrir.Application.Game.Tests.Progression;

public class TowerWarStateTests
{
    [Fact]
    public void FreshState_EveryTowerIsUntouchedAndInvalid()
    {
        var state = new TowerWarState();

        for (var i = 0; i < TowerWarState.TowerCount; i++)
        {
            Assert.Equal(0, state.GetPackedState(i));
            Assert.False(state.IsValid(i));
        }
    }

    [Theory]
    [InlineData(201, 2, 1)]
    [InlineData(402, 4, 2)]
    [InlineData(603, 6, 3)]
    [InlineData(801, 8, 1)]
    public void DecodeLevelAndType_SplitThePackedValue(int packed, int expectedLevel, int expectedType)
    {
        Assert.Equal(expectedLevel, TowerWarState.DecodeLevel(packed));
        Assert.Equal(expectedType, TowerWarState.DecodeType(packed));
    }

    [Fact]
    public void PackedValueBelowOne_DecodesToZeroLevelAndType()
    {
        Assert.Equal(0, TowerWarState.DecodeLevel(0));
        Assert.Equal(0, TowerWarState.DecodeType(0));
    }

    [Fact]
    public void SetTowerState_IsReflectedByGetters()
    {
        var state = new TowerWarState();

        state.SetTowerState(5, 201, true);

        Assert.Equal(201, state.GetPackedState(5));
        Assert.True(state.IsValid(5));
        Assert.Equal(0, state.GetPackedState(4));
    }

    [Fact]
    public void MarkUpgradeSubmitted_ClearsOnlyValid_PackedStateUnchanged()
    {
        var state = new TowerWarState();
        state.SetTowerState(3, 201, true);

        state.MarkUpgradeSubmitted(3);

        Assert.False(state.IsValid(3));
        Assert.Equal(201, state.GetPackedState(3));
    }
}
