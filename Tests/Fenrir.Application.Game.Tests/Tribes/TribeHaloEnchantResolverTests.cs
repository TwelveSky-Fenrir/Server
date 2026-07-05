using Fenrir.Application.Game.Combat;
using Fenrir.Application.Game.Tribes;

namespace Fenrir.Application.Game.Tests.Tribes;

public class TribeHaloEnchantResolverTests
{
    [Theory]
    [InlineData(0, 15, 3)] // pCurrentImprove=1 -> bracket 1-9
    [InlineData(8, 15, 3)] // pCurrentImprove=9 -> still bracket 1-9
    [InlineData(9, 15, 6)] // pCurrentImprove=10 -> bracket 10-19
    [InlineData(19, 15, 9)] // pCurrentImprove=20 -> bracket 20-29 boundary check
    [InlineData(29, 15, 12)] // pCurrentImprove=30 -> bracket 30-39
    [InlineData(88, 15, 27)] // pCurrentImprove=89 -> bracket 80-89
    [InlineData(89, 15, 30)] // pCurrentImprove=90 -> falls to the >=90 default bracket
    [InlineData(95, 15, 30)] // aHalo cap boundary (96 itself is blocked by the caller, not this table)
    public void GetRates_MatchesTheVerifiedLegacyTable(int currentHalo, int expectedSuccess, int expectedDecrease)
    {
        var (success, decrease) = TribeHaloEnchantResolver.GetRates(currentHalo);

        Assert.Equal(expectedSuccess, success);
        Assert.Equal(expectedDecrease, decrease);
    }

    [Fact]
    public void Resolve_FirstRollBelowThreshold_Succeeds_AndIncrementsHalo()
    {
        // successThreshold = 15 + 2 = 17 -- a roll of 16 is a success.
        var random = new ScriptedRandomSource([16]);

        var (outcome, newHalo, newProtect) = TribeHaloEnchantResolver.Resolve(10, 2, random);

        Assert.Equal(TribeHaloEnchantOutcome.Success, outcome);
        Assert.Equal(11, newHalo);
        Assert.Equal(2, newProtect); // untouched on success
    }

    [Fact]
    public void Resolve_FirstRollFails_SecondRollBelowDecrease_WithProtectionCharge_ConsumesProtection()
    {
        // successThreshold at halo=10 is 17; decreaseRate is 3 (bracket 1-9 for pCurrentImprove=11).
        var random = new ScriptedRandomSource([50, 1]);

        var (outcome, newHalo, newProtect) = TribeHaloEnchantResolver.Resolve(10, 2, random);

        Assert.Equal(TribeHaloEnchantOutcome.ProtectionConsumed, outcome);
        Assert.Equal(10, newHalo); // unchanged -- protection absorbed the downgrade
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
        // no explicit halo==0 downgrade branch -- falls straight through to the neutral-fail path
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

    /// <summary>Replays a fixed sequence of draws -- same pattern as other Combat resolver tests in this suite.</summary>
    private sealed class ScriptedRandomSource(int[] draws) : IRandomSource
    {
        private int _index;

        public int NextInt32(int exclusiveUpperBound)
        {
            return draws[_index++];
        }
    }
}
