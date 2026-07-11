using Fenrir.Application.Game.Domain.Combat;

namespace Fenrir.Application.Game.Tests.Combat;

public class PvpKillContributionPointBonusesTests
{
    [Fact]
    public void ComputeGameWideAddValue_DoublesTheConfiguredValueForRebirth()
    {
        Assert.Equal(6, PvpKillContributionPointBonuses.ComputeGameWideAddValue(3));
        Assert.Equal(0, PvpKillContributionPointBonuses.ComputeGameWideAddValue(0));
    }

    [Fact]
    public void ComputePerUserAddValue_OneWhenTimeEffectActive_ElseZero()
    {
        Assert.Equal(PvpKillContributionPointBonuses.PerUserCrossTribeAddBonus,
            PvpKillContributionPointBonuses.ComputePerUserAddValue(true));
        Assert.Equal(0, PvpKillContributionPointBonuses.ComputePerUserAddValue(false));
    }

    [Fact]
    public void Server160Bonus_FiresWhenAttackerTribeMatchesAddedCpTribe()
    {
        var bonus = PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            160, 2, 2, 1, false);

        Assert.Equal(PvpKillContributionPointBonuses.Server160AddedTribeBonus, bonus);
    }

    [Fact]
    public void Server160Bonus_WithheldWhenNoAddedCpTribeDesignated()
    {
        var bonus = PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            160, 0, -1, 1, false);

        Assert.Equal(0, bonus);
    }

    [Fact]
    public void Server160Bonus_WithheldWhenTribeDoesNotMatch()
    {
        var bonus = PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            160, 1, 2, 1, false);

        Assert.Equal(0, bonus);
    }

    [Fact]
    public void SymbolBattleBonus_FiresOnServer38AtBaseLevel135WithBattleActive()
    {
        var bonus = PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            38, 0, -1,
            PvpKillContributionPointBonuses.SymbolBattleMinimumBaseLevel,
            true);

        Assert.Equal(PvpKillContributionPointBonuses.SymbolBattleBaseLevelBonus, bonus);
    }

    [Fact]
    public void SymbolBattleBonus_WithheldBelowBaseLevel135()
    {
        var bonus = PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            38, 0, -1, 134, true);

        Assert.Equal(0, bonus);
    }

    [Fact]
    public void SymbolBattleBonus_WithheldWhenBattleInactive()
    {
        var bonus = PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            38, 0, -1, 140, false);

        Assert.Equal(0, bonus);
    }

    [Theory]
    [InlineData((short)1)]
    [InlineData((short)6)]
    [InlineData((short)11)]
    [InlineData((short)140)]
    public void MinorityCapitalBonus_FiresWhenTribeIsNotTheServerHomeTribe(short mapId)
    {
        var bonus = PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            mapId, 0, -1, 1, false,
            1);

        Assert.Equal(PvpKillContributionPointBonuses.MinorityCapitalBonus, bonus);
    }

    [Fact]
    public void MinorityCapitalBonus_WithheldForTheServerHomeTribe()
    {
        var bonus = PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            1, 1, -1, 1, false,
            1);

        Assert.Equal(0, bonus);
    }

    [Fact]
    public void MinorityCapitalBonus_WithheldWhenHomeTribeMappingUnknown()
    {
        var bonus = PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            1, 0, -1, 1, false);

        Assert.Equal(0, bonus);
    }

    [Fact]
    public void UnlistedServer_GrantsNoConditionalBonus()
    {
        var bonus = PvpKillContributionPointBonuses.ComputeConditionalBonuses(
            999, 0, 0, 200, true,
            3);

        Assert.Equal(0, bonus);
    }
}
