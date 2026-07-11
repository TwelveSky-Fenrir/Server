using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Application.Game.Stats.Context;
using Fenrir.Data.Abstractions.World;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Workstream B2 -- the five persisted elixir/potion counters feeding recomputed DERIVED stats
///     (<c>MyFactor::GetZoneForElixir</c>, Server/Header/Protocol/MyFactor.cpp:549-739). Every contribution is a
///     flat, additive whole-number amount (never a multiplier, never a base-attribute feed), so these are pure
///     int-truncation-exact reference vectors like <see cref="StatCalculatorTests" />.
///     <para>
///         Reachability note: every contribution getter wired this workstream is exercised through the public
///         <see cref="StatCalculator.ComputeBaseStats" />. Max HP / Max MP receive both the consumable and zone
///         contexts, element attack/defense receive both, and -- since the call-site wiring pass landed --
///         <c>ComputeBaseStats</c> now also threads the consumable/zone contexts into the strength-elixir (attack
///         power) and dexterity-elixir (accuracy/block) feeds, so all of those contributions are fully driven here.
///     </para>
/// </summary>
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

    // ---- Life-elixir -> max HP (+20 / elixir, MyFactor.cpp:595,600) ----

    [Fact]
    public void MaxLife_LifeElixirInEligibleZone_AddsTwentyPerElixir()
    {
        // base MaxLife = (int)(100*20)=2000 + level factor 50 = 2050 (see StatCalculatorTests); elixir adds flat.
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1, life: 50));
        var consumable = new ConsumableContext(EatLifePotion: 200);
        var zone = new ZoneContext(ZoneNumber: 1);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable,
            zone: zone);

        Assert.Equal(2050 + 20 * 200, stats.MaxLife); // 2050 + 4000 = 6050
    }

    [Fact]
    public void MaxLife_LifeElixirAtGradeTwelveCap_AddsTwentyTimesFourHundred()
    {
        // grade-12/high-level cap is 400 (contract Edge cases) -- the read site enforces no range, so 400 is legal.
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1, life: 50));
        var consumable = new ConsumableContext(EatLifePotion: 400);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(2050 + 20 * 400, stats.MaxLife); // 2050 + 8000 = 10050
    }

    [Fact]
    public void MaxLife_LifeElixirZoneNumberInReEnableBand_StillApplies()
    {
        // 319..323 is the concrete re-enable band -- eligibility holds there.
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1, life: 50));
        var consumable = new ConsumableContext(EatLifePotion: 10);

        foreach (short bandZone in (short[])[319, 320, 323])
        {
            var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable,
                zone: new ZoneContext(ZoneNumber: bandZone));
            Assert.Equal(2050 + 20 * 10, stats.MaxLife); // 2250
        }
    }

    [Fact]
    public void MaxLife_NoLifeElixir_MatchesEquipmentOnlyBaseline()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1, life: 50));

        var baseline = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);
        var withDefaultConsumable = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels,
            consumable: default, zone: new ZoneContext(ZoneNumber: 1));

        Assert.Equal(2050, baseline.MaxLife);
        Assert.Equal(baseline.MaxLife, withDefaultConsumable.MaxLife); // a zero counter contributes nothing
    }

    // ---- Mana-elixir -> max MP (+25 / elixir, MyFactor.cpp:627,632) ----

    [Fact]
    public void MaxMana_ManaElixirInEligibleZone_AddsTwentyFivePerElixir()
    {
        // base MaxMana = (int)(50*15.3100004196167f)=765 + mana factor 100 = 865 (see StatCalculatorTests).
        var attributes = Attributes(intelligence: 50, level: 1);
        var levels = Levels(LevelRow(1, mana: 100));
        var consumable = new ConsumableContext(EatManaPotion: 200);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable,
            zone: new ZoneContext(ZoneNumber: 1));

        Assert.Equal(865 + 25 * 200, stats.MaxMana); // 865 + 5000 = 5865
    }

    // ---- Elemental-elixir packed counter -> element attack / defense (+10 / count, MyFactor.cpp:560,562) ----
    // Packed encoding (MyUtil::GetEatElementPotion, S07_MyGame03.cpp:9132-9139): attack count = value / 1000,
    // defense count = value % 1000. Driven through ComputeBaseStats (base layer, no title multiplier).

    [Fact]
    public void ElementAttackAndDefense_PackedCounter_DecodesThousandsAndRemainderIndependently()
    {
        // packed 5003 -> attack count 5, defense count 3 -> +50 element attack, +30 element defense.
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        var consumable = new ConsumableContext(EatElePotion: 5003);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(10 * 5, stats.ElementAttackPower); // 50
        Assert.Equal(10 * 3, stats.ElementDefensePower); // 30
    }

    [Fact]
    public void ElementAttackAndDefense_BothSubCountsAtCap_AddTenTimesFourHundredEach()
    {
        // packed 400400 -> attack 400, defense 400 (each capped at the grade-12 tier) -> +4000 / +4000.
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        var consumable = new ConsumableContext(EatElePotion: 400_400);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(10 * 400, stats.ElementAttackPower); // 4000
        Assert.Equal(10 * 400, stats.ElementDefensePower); // 4000
    }

    [Fact]
    public void ElementAttack_AttackOnlyPackedValue_LeavesDefenseAtZero()
    {
        // packed 7000 -> attack 7, defense 0.
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        var consumable = new ConsumableContext(EatElePotion: 7000);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(10 * 7, stats.ElementAttackPower); // 70
        Assert.Equal(0, stats.ElementDefensePower);
    }

    [Fact]
    public void ElementDefense_DefenseOnlyPackedValue_LeavesAttackAtZero()
    {
        // packed 250 -> attack 0, defense 250.
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        var consumable = new ConsumableContext(EatElePotion: 250);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(0, stats.ElementAttackPower);
        Assert.Equal(10 * 250, stats.ElementDefensePower); // 2500
    }

    [Fact]
    public void ElementAttack_LevelFactorPlusElixir_AddCleanly()
    {
        // element attack keeps its level factor; the elixir count is a separate flat add on top.
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1, elementAttack: 15));
        var consumable = new ConsumableContext(EatElePotion: 4000); // attack count 4

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable);

        Assert.Equal(15 + 10 * 4, stats.ElementAttackPower); // 55
    }

    // ---- All non-blocked families combine in one recompute ----

    [Fact]
    public void ComputeBaseStats_LifeManaAndElementElixirs_EachFoldsIntoItsOwnStatIndependently()
    {
        var attributes = Attributes(100, intelligence: 50, level: 1);
        var levels = Levels(LevelRow(1, life: 50, mana: 100));
        var consumable = new ConsumableContext(
            EatLifePotion: 30,
            EatManaPotion: 40,
            EatElePotion: 12_009); // attack 12, defense 9
        var zone = new ZoneContext(ZoneNumber: 1);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, consumable: consumable,
            zone: zone);

        Assert.Equal(2050 + 20 * 30, stats.MaxLife); // 2650
        Assert.Equal(865 + 25 * 40, stats.MaxMana); // 1865
        Assert.Equal(10 * 12, stats.ElementAttackPower); // 120
        Assert.Equal(10 * 9, stats.ElementDefensePower); // 90
    }

    // ---- Discrepancy guard: the contribution is a FLAT additive amount, never a multiplier ----

    [Fact]
    public void MaxLife_LifeElixirContribution_IsFlatAdditive_NotScaledByBaseLife()
    {
        // Contract Edge cases / discrepancy note (Server/ wins): the source finding paraphrased this as a
        // "HP x1.2" boost, but the cited legacy folds a flat +20/elixir on top of the stat from every other
        // source -- it is never a multiplier. Proof: the same counter adds the identical delta regardless of
        // the base MaxLife it lands on. A 1.2x scale would make the high-base delta larger than the low-base
        // one; a flat add keeps them equal.
        var levels = Levels(LevelRow(1, life: 50));
        var consumable = new ConsumableContext(EatLifePotion: 150);
        var zone = new ZoneContext(ZoneNumber: 1);

        var lowBase = StatCalculator.ComputeBaseStats(Attributes(100, level: 1), NoEquipment, levels,
            consumable: consumable, zone: zone);
        var lowBaseNoElixir = StatCalculator.ComputeBaseStats(Attributes(100, level: 1), NoEquipment, levels,
            zone: zone);
        var highBase = StatCalculator.ComputeBaseStats(Attributes(500, level: 1), NoEquipment, levels,
            consumable: consumable, zone: zone);
        var highBaseNoElixir = StatCalculator.ComputeBaseStats(Attributes(500, level: 1), NoEquipment, levels,
            zone: zone);

        Assert.Equal(20 * 150, lowBase.MaxLife - lowBaseNoElixir.MaxLife); // 3000 on a 2050 base
        Assert.Equal(20 * 150, highBase.MaxLife - highBaseNoElixir.MaxLife); // identical 3000 on a 10050 base
    }

    // ---- Strength/dexterity elixir feeds (reachable now the call-site wiring pass threaded the context) ----

    [Fact]
    public void AttackPower_StrengthElixirInEligibleZone_AddsThreePerElixir()
    {
        // ComputeAttackPower implements the +3/elixir strength feed (MyFactor.cpp:657,662), and ComputeBaseStats
        // now threads the consumable/zone contexts into it (the call-site wiring pass landed). In an
        // elixir-eligible zone a populated strength counter adds a flat +3 per elixir on top of the baseline.
        var attributes = Attributes(strength: 100, level: 1);
        var levels = Levels(LevelRow(1));
        var zone = new ZoneContext(ZoneNumber: 1);

        var baseline = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, zone: zone);
        var withStrElixir = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels,
            consumable: new ConsumableContext(EatStrPotion: 200), zone: zone);

        Assert.Equal(baseline.AttackPower + 3 * 200, withStrElixir.AttackPower);
    }

    [Fact]
    public void AccuracyAndBlock_DexterityElixirInEligibleZone_AddTwoPerElixir()
    {
        // The dexterity elixir feeds both accuracy (+2/elixir, MyFactor.cpp:694,699) and block (+2/elixir,
        // MyFactor.cpp:726,731) via ComputeAttackSuccess/ComputeAttackBlock; ComputeBaseStats now threads the
        // consumable/zone contexts into both (the call-site wiring pass landed). In an elixir-eligible zone a
        // populated dexterity counter adds a flat +2 per elixir to each stat.
        var attributes = Attributes(strength: 50, dexterity: 50, level: 1);
        var levels = Levels(LevelRow(1));
        var zone = new ZoneContext(ZoneNumber: 1);

        var baseline = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, zone: zone);
        var withDexElixir = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels,
            consumable: new ConsumableContext(EatDexPotion: 200), zone: zone);

        Assert.Equal(baseline.AttackSuccess + 2 * 200, withDexElixir.AttackSuccess);
        Assert.Equal(baseline.AttackBlock + 2 * 200, withDexElixir.AttackBlock);
    }
}
