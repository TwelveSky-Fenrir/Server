using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

public class B12GetterFixTests
{
    private static readonly EquippedItemSlot[] NoEquipment = [];


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
    [InlineData((short)124, false)]
    public void IsFreeForAllZone_IsExactEqualityWith335(short zone, bool expected)
    {
        Assert.Equal(expected, StatCalculator.IsFreeForAllZone(zone));
    }

    [Fact]
    public void ApplyFreeForAllMaxLife_InArena_DiscardsComputedAndReturnsFixedConstant()
    {
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


    [Theory]
    [InlineData(76005, 0)]
    [InlineData(76006, 1000)]
    [InlineData(76007, 2000)]
    [InlineData(76004, 0)]
    [InlineData(0, 0)]
    public void PhoenixDamageSecondPassBonus_MatchesContractSeries(int itemId, int expected)
    {
        Assert.Equal(expected, StatCalculator.PhoenixDamageSecondPassBonus(itemId));
    }


    [Theory]
    [InlineData((short)19, true)]
    [InlineData((short)175, true)]
    [InlineData((short)193, true)]
    [InlineData((short)18, false)]
    [InlineData((short)22, false)]
    [InlineData((short)335, false)]
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
    [InlineData(157, 157)]
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
    [InlineData(157, 1)]
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
    [InlineData(157, 1)]
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


    [Theory]
    [InlineData(76005, 7000)]
    [InlineData(76006, 12000)]
    [InlineData(76007, 22000)]
    public void PhoenixDefense_NetIsWithdrawalCancelledPlusBothPasses(int phoenixId, int expectedDefense)
    {
        var attributes = Attributes();
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(8, Item(phoenixId, defensePower: 250))];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(expectedDefense, stats.DefensePower);
    }


    [Theory]
    [InlineData(76005, 7000)]
    [InlineData(76006, 12000)]
    [InlineData(76007, 22000)]
    public void PhoenixMaxLife_TwoPassGivesCombinedTotal_NoWithdrawal(int phoenixId, int expectedLife)
    {
        var attributes = Attributes();
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(8, Item(phoenixId, defensePower: 250, attackPower: 250))];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(expectedLife, stats.MaxLife);
    }

    [Theory]
    [InlineData(76005, 7000)]
    [InlineData(76006, 12000)]
    [InlineData(76007, 22000)]
    public void PhoenixMaxMana_TwoPassGivesCombinedTotal_NoWithdrawal(int phoenixId, int expectedMana)
    {
        var attributes = Attributes();
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(8, Item(phoenixId))];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(expectedMana, stats.MaxMana);
    }


    [Fact]
    public void MaxLifeSetItemBonus_Set20_GrantsTwentyThousandChainedWithFifteenThousand()
    {
        var attributes = Attributes();
        var levels = Levels(LevelRow(1));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, 20);

        Assert.Equal(35000, stats.MaxLife);
    }

    [Fact]
    public void MaxLifeSetItemBonus_AnyNonZeroSet_GrantsFifteenThousandOnly()
    {
        var attributes = Attributes();
        var levels = Levels(LevelRow(1));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, 5);

        Assert.Equal(15000, stats.MaxLife);
    }


    [Fact]
    public void KiReadsIntelligence_WisdomReadsDexterity()
    {
        var levels = Levels(LevelRow(1));

        var intOnly = StatCalculator.ComputeBaseStats(Attributes(intelligence: 100), NoEquipment, levels);
        Assert.Equal(1531, intOnly.MaxMana);
        Assert.Equal(0, intOnly.DefensePower);

        var dexOnly = StatCalculator.ComputeBaseStats(Attributes(dexterity: 200), NoEquipment, levels);
        Assert.Equal(1926, dexOnly.DefensePower);
        Assert.Equal(0, dexOnly.MaxMana);
    }


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
