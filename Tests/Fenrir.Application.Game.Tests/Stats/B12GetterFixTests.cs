using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B12 getter-fix coverage for the MyFactor stat surface. Two kinds of test live here:
///     <list type="bullet">
///         <item>
///             <b>Fix-logic tests</b> drive the new public helpers on
///             <see cref="StatCalculator" /> (<c>StatCalculator.B12getterfixesFix.cs</c>) directly -- the FFA
///             max-life/max-mana terminal override and the Phoenix damage second pass. These are the extracted
///             correction logic; the getter bodies invoke them via the one-line edits in the workstream's
///             wiring manifest (the same "test the accessor directly, wire the call site separately" shape the
///             RankBuff/StellarCore contribution tests already use).
///         </item>
///         <item>
///             <b>Regression tests</b> drive <see cref="StatCalculator.ComputeBaseStats" /> end-to-end to lock
///             the halves of the contract Fenrir already implements correctly (Phoenix defense two-pass with
///             withdrawal, Phoenix single-additive max-life/max-mana, the set-20 chained life bonus, and the
///             Ki=Intelligence / Wisdom=Dexterity name swap) so a later edit can't regress them.
///         </item>
///     </list>
///     Hand-computed reference vectors, not the implementation's echo. Inputs use comfortable fractional parts
///     so MyFactor's non-representable float coefficients (9.63f, 15.3100004196167f) never truncate ambiguously.
/// </summary>
public class B12GetterFixTests
{
    private static readonly EquippedItemSlot[] NoEquipment = [];

    // ---- Free-for-all (zone 335) terminal max-life / max-mana override ----

    [Fact]
    public void FreeForAll_Constants_MatchContract()
    {
        Assert.Equal((short)335, StatCalculator.FreeForAllZoneNumber);
        Assert.Equal(100000, StatCalculator.FreeForAllMaxLife);
        Assert.Equal(30000, StatCalculator.FreeForAllMaxMana);
    }

    [Theory]
    [InlineData((short)335, true)]
    [InlineData((short)334, false)]
    [InlineData((short)336, false)]
    [InlineData((short)0, false)]
    [InlineData((short)124, false)] // the rank-buff-suppressed zone is NOT the FFA zone
    public void IsFreeForAllZone_IsExactEqualityWith335(short zone, bool expected)
    {
        Assert.Equal(expected, StatCalculator.IsFreeForAllZone(zone));
    }

    [Fact]
    public void ApplyFreeForAllMaxLife_InArena_DiscardsComputedAndReturnsFixedConstant()
    {
        // Any computed value, however large or small, is discarded for the flat 100000 inside zone 335.
        Assert.Equal(100000, StatCalculator.ApplyFreeForAllMaxLife(1, 335));
        Assert.Equal(100000, StatCalculator.ApplyFreeForAllMaxLife(999_999, 335));
    }

    [Fact]
    public void ApplyFreeForAllMaxLife_OutsideArena_PassesComputedThrough()
    {
        Assert.Equal(4275, StatCalculator.ApplyFreeForAllMaxLife(4275, 100));
        Assert.Equal(4275, StatCalculator.ApplyFreeForAllMaxLife(4275, 334));
    }

    [Fact]
    public void ApplyFreeForAllMaxMana_InArena_DiscardsComputedAndReturnsFixedConstant()
    {
        Assert.Equal(30000, StatCalculator.ApplyFreeForAllMaxMana(1, 335));
        Assert.Equal(30000, StatCalculator.ApplyFreeForAllMaxMana(999_999, 335));
    }

    [Fact]
    public void ApplyFreeForAllMaxMana_OutsideArena_PassesComputedThrough()
    {
        Assert.Equal(1531, StatCalculator.ApplyFreeForAllMaxMana(1531, 100));
    }

    // ---- Phoenix Amulet damage SECOND pass (the missing +0/+1000/+2000, MyFactor.cpp:2699-2711) ----

    [Theory]
    [InlineData(76005, 0)]
    [InlineData(76006, 1000)]
    [InlineData(76007, 2000)]
    [InlineData(76004, 0)] // adjacent non-Phoenix pet id
    [InlineData(0, 0)]
    public void PhoenixDamageSecondPassBonus_MatchesContractSeries(int itemId, int expected)
    {
        Assert.Equal(expected, StatCalculator.PhoenixDamageSecondPassBonus(itemId));
    }

    // ---- Balance-Zone normalization tables (blocked on a stat-balance flag; pure data verified here) ----

    [Theory]
    [InlineData((short)19, true)]
    [InlineData((short)175, true)]
    [InlineData((short)193, true)]
    [InlineData((short)18, false)] // one below the first entry
    [InlineData((short)22, false)] // one above the 19-21 band
    [InlineData((short)335, false)] // FFA zone is deliberately NOT a Balance-Zone (mutually exclusive)
    [InlineData((short)0, false)]
    public void IsBalanceStatZone_MatchesLegacyMembership(short zone, bool expected)
    {
        Assert.Equal(expected, StatCalculator.IsBalanceStatZone(zone));
    }

    [Fact]
    public void IsBalanceStatZone_CoversExactlyTheFourteenLegacyZones()
    {
        short[] expected = [19, 20, 21, 34, 49, 120, 154, 175, 176, 177, 190, 191, 192, 193];
        foreach (var zone in expected)
            Assert.True(StatCalculator.IsBalanceStatZone(zone));
    }

    [Theory]
    [InlineData(1, 112)]
    [InlineData(112, 112)]
    [InlineData(113, 145)]
    [InlineData(145, 145)]
    [InlineData(146, 156)]
    [InlineData(156, 156)]
    [InlineData(157, 157)] // above 156 -> the actual level
    [InlineData(200, 200)]
    public void BalanceLevelTerm_MatchesThresholdTable(int level, int expected)
    {
        Assert.Equal(expected, StatCalculator.BalanceLevelTerm(level));
    }

    [Theory]
    [InlineData(100, 336)]
    [InlineData(112, 336)]
    [InlineData(113, 842)]
    [InlineData(145, 842)]
    [InlineData(146, 1420)]
    [InlineData(156, 1420)]
    [InlineData(157, 1)] // above 156 -> 1
    public void BalanceVitality_MatchesLevelScaledTable(int level, int expected)
    {
        Assert.Equal(expected, StatCalculator.BalanceVitality(level));
    }

    [Theory]
    [InlineData(100, 351)]
    [InlineData(112, 351)]
    [InlineData(113, 886)]
    [InlineData(145, 886)]
    [InlineData(146, 1411)]
    [InlineData(156, 1411)]
    [InlineData(157, 1)] // above 156 -> 1
    public void BalanceStrength_MatchesLevelScaledTable(int level, int expected)
    {
        Assert.Equal(expected, StatCalculator.BalanceStrength(level));
    }

    [Fact]
    public void BalanceAttributes_IntelligenceAndDexterityAreAlwaysOne_VitStrLevelScaled()
    {
        var low = StatCalculator.BalanceAttributes(100);
        Assert.Equal((336, 351, 1, 1), low);

        var mid = StatCalculator.BalanceAttributes(130);
        Assert.Equal((842, 886, 1, 1), mid);

        var high = StatCalculator.BalanceAttributes(150);
        Assert.Equal((1420, 1411, 1, 1), high);

        var beyond = StatCalculator.BalanceAttributes(160);
        Assert.Equal((1, 1, 1, 1), beyond);
    }

    // ---- Regression: Phoenix defense two-pass (withdrawal + 5000/7500/12500 then 2000/4500/9500) ----
    // Already correct in StatCalculator.DefensePower.cs; locked end-to-end via ComputeBaseStats. The pet's own
    // raw defense (250 below) is added by the equipment loop then withdrawn, so the net is purely the two
    // Phoenix passes: +7000 / +12000 / +22000.

    [Theory]
    [InlineData(76005, 7000)]
    [InlineData(76006, 12000)]
    [InlineData(76007, 22000)]
    public void PhoenixDefense_NetIsWithdrawalCancelledPlusBothPasses(int phoenixId, int expectedDefense)
    {
        var attributes = Attributes(); // wisdom(dex)=0 so base def is 0
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(8, Item(phoenixId, defensePower: 250))];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(expectedDefense, stats.DefensePower);
    }

    // ---- Regression: Phoenix max-life / max-mana single additive pass, NO withdrawal (2000/4500/9500) ----

    [Theory]
    [InlineData(76005, 2000)]
    [InlineData(76006, 4500)]
    [InlineData(76007, 9500)]
    public void PhoenixMaxLife_SingleAdditivePass_NoWithdrawal(int phoenixId, int expectedLife)
    {
        var attributes = Attributes(); // vitality=0 so base life is 0
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(8, Item(phoenixId, defensePower: 250, attackPower: 250))];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(expectedLife, stats.MaxLife);
    }

    [Theory]
    [InlineData(76005, 2000)]
    [InlineData(76006, 4500)]
    [InlineData(76007, 9500)]
    public void PhoenixMaxMana_SingleAdditivePass_NoWithdrawal(int phoenixId, int expectedMana)
    {
        var attributes = Attributes(); // ki(int)=0 so base mana is 0
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(8, Item(phoenixId))];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(expectedMana, stats.MaxMana);
    }

    // ---- Regression: max-life set-item bonus, set 20 -> +20000 CHAINED with the "any set>0" +15000 ----

    [Fact]
    public void MaxLifeSetItemBonus_Set20_GrantsTwentyThousandChainedWithFifteenThousand()
    {
        var attributes = Attributes(); // vitality=0, no equipment => life is purely the set bonus
        var levels = Levels(LevelRow(1));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, legacySetNumber: 20);

        Assert.Equal(35000, stats.MaxLife); // 20000 (set==20) + 15000 (any set>0), chained
    }

    [Fact]
    public void MaxLifeSetItemBonus_AnyNonZeroSet_GrantsFifteenThousandOnly()
    {
        var attributes = Attributes();
        var levels = Levels(LevelRow(1));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, legacySetNumber: 5);

        Assert.Equal(15000, stats.MaxLife); // set 5 grants no flat set-life of its own, only the any-set +15000
    }

    // ---- Regression: the Ki=Intelligence / Wisdom=Dexterity name swap MUST be preserved ----

    [Fact]
    public void KiReadsIntelligence_WisdomReadsDexterity()
    {
        var levels = Levels(LevelRow(1));

        // Intelligence feeds Ki -> MaxMana (ki * 15.31); Dexterity feeds Wisdom -> DefensePower (wisdom * 9.63).
        var intOnly = StatCalculator.ComputeBaseStats(Attributes(intelligence: 100), NoEquipment, levels);
        Assert.Equal(1531, intOnly.MaxMana); // (int)(100 * 15.3100004196167)
        Assert.Equal(0, intOnly.DefensePower); // dexterity(=Wisdom) was 0

        var dexOnly = StatCalculator.ComputeBaseStats(Attributes(dexterity: 200), NoEquipment, levels);
        Assert.Equal(1926, dexOnly.DefensePower); // (int)(200 * 9.63)
        Assert.Equal(0, dexOnly.MaxMana); // intelligence(=Ki) was 0
    }

    // ---- fixtures (mirrors StatCalculatorTests' private helpers) ----

    private static CharacterBaseAttributes Attributes(
        int vitality = 0, int strength = 0, int intelligence = 0, int dexterity = 0,
        short level = 1, byte tribe = 0, byte? previousTribe = null, int title = 0, int halo = 0,
        int rebirthCount = 0)
    {
        return new CharacterBaseAttributes(vitality, strength, intelligence, dexterity, level, tribe,
            previousTribe ?? tribe, title, halo, rebirthCount);
    }

    private static FrozenDictionary<short, LevelRowDto> Levels(params LevelRowDto[] rows)
    {
        var dict = new Dictionary<short, LevelRowDto>();
        foreach (var row in rows) dict[row.Level] = row;
        return dict.ToFrozenDictionary();
    }

    private static LevelRowDto LevelRow(short level, int life = 0, int mana = 0)
    {
        return new LevelRowDto(level, 0, 100, 0, 0, 0, 0, 0, 0, life, mana);
    }

    private static ItemRowDto Item(int itemId, byte sort = 0, short attackPower = 0, short defensePower = 0)
    {
        return new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            0, sort, 0, 0, 0,
            1, 0, 0, 0,
            0, 0, 0, 1, 0,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, 0, 0,
            0, 0, 0, 0, 0,
            attackPower, defensePower, 0, 0,
            0, 0, 0,
            0, 0, null,
            0, 0, 0, 0, 0);
    }

    private static EquippedItemSlot Equip(int slotIndex, ItemRowDto item, byte enchant = 0, byte combine = 0)
    {
        return new EquippedItemSlot(slotIndex, item, enchant, combine, 0, 0);
    }
}
