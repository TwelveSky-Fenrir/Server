using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

public class ConsumableElixirStatFeedTests
{
    private static readonly EquippedItemSlot[] NoEquipment = [];

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

    private static LevelRowDto LevelRow(short level, int life = 0, int mana = 0, short elementAttack = 0)
    {
        return new LevelRowDto(level, 0, 100, 0, 0, 0, 0, 0, elementAttack, life, mana);
    }


    [Fact]
    public void MaxLife_LifeElixirInEligibleZone_AddsTwentyPerElixir()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1, 50));
        var consumable = new ConsumableContext(200);
        var zone = new ZoneContext(1);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable,
            zone: zone);

        Assert.Equal(2050 + 20 * 200, stats.MaxLife);
    }

    [Fact]
    public void MaxLife_LifeElixirAtGradeTwelveCap_AddsTwentyTimesFourHundred()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1, 50));
        var consumable = new ConsumableContext(400);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(2050 + 20 * 400, stats.MaxLife);
    }

    [Fact]
    public void MaxLife_LifeElixirZoneNumberInReEnableBand_StillApplies()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1, 50));
        var consumable = new ConsumableContext(10);

        foreach (var bandZone in (short[])[319, 320, 323])
        {
            var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable,
                zone: new ZoneContext(bandZone));
            Assert.Equal(2050 + 20 * 10, stats.MaxLife);
        }
    }

    [Fact]
    public void MaxLife_NoLifeElixir_MatchesEquipmentOnlyBaseline()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1, 50));

        var baseline = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);
        var withDefaultConsumable = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels,
            consumable: default, zone: new ZoneContext(1));

        Assert.Equal(2050, baseline.MaxLife);
        Assert.Equal(baseline.MaxLife, withDefaultConsumable.MaxLife);
    }


    [Fact]
    public void MaxMana_ManaElixirInEligibleZone_AddsTwentyFivePerElixir()
    {
        var attributes = Attributes(intelligence: 50, level: 1);
        var levels = Levels(LevelRow(1, mana: 100));
        var consumable = new ConsumableContext(EatManaPotion: 200);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable,
            zone: new ZoneContext(1));

        Assert.Equal(865 + 25 * 200, stats.MaxMana);
    }


    [Fact]
    public void ElementAttackAndDefense_PackedCounter_DecodesThousandsAndRemainderIndependently()
    {
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        var consumable = new ConsumableContext(EatElePotion: 5003);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(10 * 5, stats.ElementAttackPower);
        Assert.Equal(10 * 3, stats.ElementDefensePower);
    }

    [Fact]
    public void ElementAttackAndDefense_BothSubCountsAtCap_AddTenTimesFourHundredEach()
    {
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        var consumable = new ConsumableContext(EatElePotion: 400_400);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(10 * 400, stats.ElementAttackPower);
        Assert.Equal(10 * 400, stats.ElementDefensePower);
    }

    [Fact]
    public void ElementAttack_AttackOnlyPackedValue_LeavesDefenseAtZero()
    {
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        var consumable = new ConsumableContext(EatElePotion: 7000);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(10 * 7, stats.ElementAttackPower);
        Assert.Equal(0, stats.ElementDefensePower);
    }

    [Fact]
    public void ElementDefense_DefenseOnlyPackedValue_LeavesAttackAtZero()
    {
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        var consumable = new ConsumableContext(EatElePotion: 250);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(0, stats.ElementAttackPower);
        Assert.Equal(10 * 250, stats.ElementDefensePower);
    }

    [Fact]
    public void ElementAttack_LevelFactorPlusElixir_AddCleanly()
    {
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1, elementAttack: 15));
        var consumable = new ConsumableContext(EatElePotion: 4000);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(15 + 10 * 4, stats.ElementAttackPower);
    }


    [Fact]
    public void ComputeBaseStats_LifeManaAndElementElixirs_EachFoldsIntoItsOwnStatIndependently()
    {
        var attributes = Attributes(100, intelligence: 50, level: 1);
        var levels = Levels(LevelRow(1, 50, 100));
        var consumable = new ConsumableContext(
            30,
            40,
            EatElePotion: 12_009);
        var zone = new ZoneContext(1);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable,
            zone: zone);

        Assert.Equal(2050 + 20 * 30, stats.MaxLife);
        Assert.Equal(865 + 25 * 40, stats.MaxMana);
        Assert.Equal(10 * 12, stats.ElementAttackPower);
        Assert.Equal(10 * 9, stats.ElementDefensePower);
    }


    [Fact]
    public void MaxLife_LifeElixirContribution_IsFlatAdditive_NotScaledByBaseLife()
    {
        var levels = Levels(LevelRow(1, 50));
        var consumable = new ConsumableContext(150);
        var zone = new ZoneContext(1);

        var lowBase = StatCalculator.ComputeBaseStats(Attributes(100, level: 1), NoEquipment, levels,
            consumable: consumable, zone: zone);
        var lowBaseNoElixir = StatCalculator.ComputeBaseStats(Attributes(100, level: 1), NoEquipment, levels,
            zone: zone);
        var highBase = StatCalculator.ComputeBaseStats(Attributes(500, level: 1), NoEquipment, levels,
            consumable: consumable, zone: zone);
        var highBaseNoElixir = StatCalculator.ComputeBaseStats(Attributes(500, level: 1), NoEquipment, levels,
            zone: zone);

        Assert.Equal(20 * 150, lowBase.MaxLife - lowBaseNoElixir.MaxLife);
        Assert.Equal(20 * 150, highBase.MaxLife - highBaseNoElixir.MaxLife);
    }


    [Fact]
    public void AttackPower_StrengthElixirInEligibleZone_AddsThreePerElixir()
    {
        var attributes = Attributes(strength: 100, level: 1);
        var levels = Levels(LevelRow(1));
        var zone = new ZoneContext(1);

        var baseline = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, zone: zone);
        var withStrElixir = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels,
            consumable: new ConsumableContext(EatStrPotion: 200), zone: zone);

        Assert.Equal(baseline.AttackPower + 3 * 200, withStrElixir.AttackPower);
    }

    [Fact]
    public void AccuracyAndBlock_DexterityElixirInEligibleZone_AddTwoPerElixir()
    {
        var attributes = Attributes(strength: 50, dexterity: 50, level: 1);
        var levels = Levels(LevelRow(1));
        var zone = new ZoneContext(1);

        var baseline = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, zone: zone);
        var withDexElixir = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels,
            consumable: new ConsumableContext(EatDexPotion: 200), zone: zone);

        Assert.Equal(baseline.AttackSuccess + 2 * 200, withDexElixir.AttackSuccess);
        Assert.Equal(baseline.AttackBlock + 2 * 200, withDexElixir.AttackBlock);
    }
}
