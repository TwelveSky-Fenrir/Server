using Fenrir.Application.Game.Domain.Combat;
using Fenrir.Application.Game.Domain.Tribes;

namespace Fenrir.Application.Game.Tests.Tribes;

public class TribeHaloEnchantResolverTests
{
    [Theory]
    [InlineData(0, 15, 3)]
    [InlineData(8, 15, 3)]
    [InlineData(9, 15, 6)]
    [InlineData(19, 15, 9)]
    [InlineData(29, 15, 12)]
    [InlineData(88, 15, 27)]
    [InlineData(89, 15, 30)]
    [InlineData(95, 15, 30)]
    public void GetRates_MatchesTheVerifiedLegacyTable(int currentHalo, int expectedSuccess, int expectedDecrease)
    {
        var (success, decrease) = TribeHaloEnchantResolver.GetRates(currentHalo);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedDecrease, decrease);
    }

    [Fact]
    public void Resolve_FirstRollBelowThreshold_Succeeds_AndIncrementsHalo()
    {
        var random = new ScriptedRandomSource([16]);

        var (outcome, newHalo, newProtect) = TribeHaloEnchantResolver.Resolve(10, 2, random);

        Assert.Equal(TribeHaloEnchantOutcome.Success, outcome);
        Assert.Equal(11, newHalo);
        Assert.Equal(2, newProtect);
    }

    [Fact]
    public void Resolve_FirstRollFails_SecondRollBelowDecrease_WithProtectionCharge_ConsumesProtection()
    {
        var random = new ScriptedRandomSource([50, 1]);

        var (outcome, newHalo, newProtect) = TribeHaloEnchantResolver.Resolve(10, 2, random);

        Assert.Equal(TribeHaloEnchantOutcome.ProtectionConsumed, outcome);
        Assert.Equal(10, newHalo);
        Assert.Equal(1, newProtect);
    }

    [Fact]
    public void Resolve_FirstRollFails_SecondRollBelowDecrease_NoProtectionCharge_Downgrades()
    {
        var random = new ScriptedRandomSource([50, 1]);

        var (outcome, newHalo, newProtect) = TribeHaloEnchantResolver.Resolve(10, 0, random);

        Assert.Equal(TribeHaloEnchantOutcome.Downgraded, outcome);
        Assert.Equal(9, newHalo);
        Assert.Equal(0, newProtect);
    }

    [Fact]
    public void Resolve_AtZeroHalo_SecondRollBelowDecrease_IsANeutralFail_NeverGoesNegative()
    {
        var random = new ScriptedRandomSource([50, 1]);

        var (outcome, newHalo, newProtect) = TribeHaloEnchantResolver.Resolve(0, 5, random);

        Assert.Equal(TribeHaloEnchantOutcome.NeutralFail, outcome);
        Assert.Equal(0, newHalo);
        Assert.Equal(5, newProtect);
    }

    [Fact]
    public void Resolve_BothRollsFail_IsANeutralFail()
    {
        var random = new ScriptedRandomSource([99, 99]);

        var (outcome, newHalo, newProtect) = TribeHaloEnchantResolver.Resolve(10, 2, random);

        Assert.Equal(TribeHaloEnchantOutcome.NeutralFail, outcome);
        Assert.Equal(10, newHalo);
        Assert.Equal(2, newProtect);
    }

    private sealed class ScriptedRandomSource(int[] draws) : IRandomSource
    {
        private int _index;

        public int NextInt32(int exclusiveUpperBound)
        {
            return draws[_index++];
        }
    }
}
