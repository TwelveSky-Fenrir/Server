using Fenrir.Application.Game.Domain.Forge;
using Fenrir.Application.Game.Tests.TestSupport;

namespace Fenrir.Application.Game.Tests.Forge;

/// <summary>
///     Covers the one cited piece of the (production-dead) refine feature: the linear
///     <c>72 - 2 * (currentRefine + addedLevel)</c> success rate and a plain success/fail roll capped at
///     <see cref="RefineResolver.MaxRefine" />. See the resolver's own remarks for why this is a new-feature
///     scaffold, not legacy parity.
/// </summary>
public class RefineResolverTests
{
    [Theory]
    [InlineData(0, 1, 70)]
    [InlineData(10, 1, 50)]
    [InlineData(24, 1, 22)]
    [InlineData(40, 1, 0)] // 72 - 82 = -10, floored at 0
    public void RefineRate_MatchesLinearFormula(int currentRefine, int addedLevel, int expected)
    {
        Assert.Equal(expected, RefineResolver.RefineRate(currentRefine, addedLevel));
    }

    [Fact]
    public void Resolve_AtCap_IsRejected()
    {
        var result = RefineResolver.Resolve(RefineResolver.MaxRefine, 1, new ScriptedRandomSource(0));

        Assert.Equal(RefineResolver.RefineOutcome.Rejected, result.Outcome);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Resolve_NonPositiveAddedLevel_IsRejected(int addedLevel)
    {
        var result = RefineResolver.Resolve(0, addedLevel, new ScriptedRandomSource(0));

        Assert.Equal(RefineResolver.RefineOutcome.Rejected, result.Outcome);
    }

    [Fact]
    public void Resolve_RollBelowRate_Succeeds_AdvancesRefine()
    {
        // rate at (0, 1) = 70; roll 0 succeeds.
        var result = RefineResolver.Resolve(0, 1, new ScriptedRandomSource(0));

        Assert.Equal(RefineResolver.RefineOutcome.Success, result.Outcome);
        Assert.Equal(1, result.NewRefine);
        Assert.Equal(70, result.Rate);
    }

    [Fact]
    public void Resolve_RollAtOrAboveRate_Fails_LeavesRefineUntouched()
    {
        var result = RefineResolver.Resolve(5, 1, new ScriptedRandomSource(99));

        Assert.Equal(RefineResolver.RefineOutcome.Failed, result.Outcome);
        Assert.Equal(5, result.NewRefine);
    }

    [Fact]
    public void Resolve_Success_ClampsNewRefineToCap()
    {
        // current 24, +5 would reach 29; clamped to MaxRefine (25). rate at (24, 5) = 72 - 58 = 14; roll 0 wins.
        var result = RefineResolver.Resolve(24, 5, new ScriptedRandomSource(0));

        Assert.Equal(RefineResolver.RefineOutcome.Success, result.Outcome);
        Assert.Equal(RefineResolver.MaxRefine, result.NewRefine);
    }
}
