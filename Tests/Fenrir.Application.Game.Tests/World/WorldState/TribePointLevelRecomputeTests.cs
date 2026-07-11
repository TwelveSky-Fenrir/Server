using Fenrir.Application.Game.Domain.World.WorldState;

namespace Fenrir.Application.Game.Tests.World.WorldState;

public class TribePointLevelRecomputeTests
{
    [Fact]
    public void EmptyRoster_EveryTribeIsExactlyBaseline_ExceptTribe3WhichAlsoGetsTheFlatBonus()
    {
        var totals = TribePointLevelRecompute.ComputeTotals([]);

        Assert.Equal(1000, totals[0]);
        Assert.Equal(1000, totals[1]);
        Assert.Equal(1000, totals[2]);
        Assert.Equal(1800, totals[3]);
    }

    [Theory]
    [InlineData(144)]
    [InlineData(0)]
    [InlineData(1)]
    public void CharacterBelowLevelThreshold_ContributesNothing(short level1)
    {
        var roster = new[] { new TribeRosterCharacterSnapshot(0, level1, 99, 99) };

        var totals = TribePointLevelRecompute.ComputeTotals(roster);

        Assert.Equal(1000, totals[0]);
    }

    [Fact]
    public void CharacterAtExactlyTheLevelThreshold_Qualifies()
    {
        var roster = new[] { new TribeRosterCharacterSnapshot(0, 145, 0, 0) };

        var totals = TribePointLevelRecompute.ComputeTotals(roster);

        Assert.Equal(1000 + 33, totals[0]);
    }

    [Fact]
    public void QualifyingCharacter_SumsAllThreeTerms()
    {
        var roster = new[] { new TribeRosterCharacterSnapshot(1, 200, 50, 4) };

        var totals = TribePointLevelRecompute.ComputeTotals(roster);

        Assert.Equal(1000 + 250, totals[1]);
        Assert.Equal(1000, totals[0]);
    }

    [Fact]
    public void MultipleQualifyingCharactersSameTribe_ContributionsAccumulate()
    {
        var roster = new[]
        {
            new TribeRosterCharacterSnapshot(2, 145, 0, 0),
            new TribeRosterCharacterSnapshot(2, 150, 10, 2)
        };

        var totals = TribePointLevelRecompute.ComputeTotals(roster);

        Assert.Equal(1000 + 33 + 74, totals[2]);
    }

    [Fact]
    public void Tribe3AloneReceivesTheFlatBonus_OnTopOfItsOwnRosterContribution()
    {
        var roster = new[] { new TribeRosterCharacterSnapshot(3, 145, 0, 0) };

        var totals = TribePointLevelRecompute.ComputeTotals(roster);

        Assert.Equal(1000 + 33 + 800, totals[3]);
        Assert.Equal(1000, totals[0]);
        Assert.Equal(1000, totals[1]);
        Assert.Equal(1000, totals[2]);
    }

    [Fact]
    public void EachTribesTotal_IsIndependentOfTheOthers()
    {
        var roster = new[]
        {
            new TribeRosterCharacterSnapshot(0, 200, 0, 0),
            new TribeRosterCharacterSnapshot(1, 145, 0, 0),
            new TribeRosterCharacterSnapshot(2, 300, 0, 0)
        };

        var totals = TribePointLevelRecompute.ComputeTotals(roster);

        Assert.Equal(1000 + 88, totals[0]);
        Assert.Equal(1000 + 33, totals[1]);
        Assert.Equal(1000 + 188, totals[2]);
        Assert.Equal(1800, totals[3]);
    }

    [Fact]
    public void OutOfRangeTribeId_IsSkippedDefensively_NeverThrows()
    {
        var roster = new[] { new TribeRosterCharacterSnapshot(4, 999, 999, 999) };

        var totals = TribePointLevelRecompute.ComputeTotals(roster);

        Assert.Equal(1000, totals[0]);
        Assert.Equal(1000, totals[1]);
        Assert.Equal(1000, totals[2]);
        Assert.Equal(1800, totals[3]);
    }

    [Fact]
    public void IsAlwaysAFullRecompute_NeverConditionalOnPriorRosterState()
    {
        var first = TribePointLevelRecompute.ComputeTotals([new TribeRosterCharacterSnapshot(0, 500, 0, 0)]);
        var second = TribePointLevelRecompute.ComputeTotals([]);

        Assert.NotEqual(first[0], second[0]);
        Assert.Equal(1000, second[0]);
    }
}
