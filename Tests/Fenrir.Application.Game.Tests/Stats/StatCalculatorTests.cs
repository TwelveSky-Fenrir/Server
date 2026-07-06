using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Stats;

/// <summary>
///     Hand-computed reference vectors for <see cref="StatCalculator" /> (MyFactor), not the implementation's own
///     echo. MyFactor's coefficients (9.63f, 3.80f, 15.3100004196167f...) aren't exactly representable in binary
///     float, so a product that looks like a clean integer (e.g. 100*3.80=380.0) can truncate one below once real
///     float rounding is accounted for -- inputs here are chosen with a comfortable (&gt;=0.25) fractional part to
///     sidestep that ambiguity.
/// </summary>
public class StatCalculatorTests
{
    private static readonly EquippedItemSlot[] NoEquipment = [];

    private static CharacterBaseAttributes Attributes(
        int vitality = 0, int strength = 0, int intelligence = 0, int dexterity = 0,
        short level = 1, byte tribe = 0, int title = 0, int halo = 0, int rebirthCount = 0)
    {
        return new CharacterBaseAttributes(vitality, strength, intelligence, dexterity, level, tribe, title, halo,
            rebirthCount);
    }

    private static FrozenDictionary<short, LevelRowDto> Levels(params LevelRowDto[] rows)
    {
        var dict = new Dictionary<short, LevelRowDto>();
        foreach (var row in rows) dict[row.Level] = row;
        return dict.ToFrozenDictionary();
    }

    private static LevelRowDto LevelRow(short level, int life = 0, int mana = 0, short attackPower = 0,
        short defensePower = 0, short attackSuccess = 0, short attackBlock = 0, short elementAttack = 0)
    {
        return new LevelRowDto(level, 0, 100, 0, attackPower, defensePower, attackSuccess, attackBlock,
            elementAttack, life, mana);
    }

    private static ItemRowDto Item(
        int itemId,
        byte sort = 0,
        short level = 1,
        byte martialLevelLimit = 0,
        byte checkSetItem = 0,
        short strength = 0,
        short dexterity = 0,
        short vitality = 0,
        short intelligent = 0,
        short luck = 0,
        short attackPower = 0,
        short defensePower = 0,
        short attackSuccess = 0,
        short attackBlock = 0,
        short elementAttackPower = 0,
        short elementDefensePower = 0,
        byte critical = 0,
        byte capeInfo2 = 0)
    {
        return new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            0, sort, 0, 0, 0,
            level, 0, 0, 0,
            0, 0, 0, 1, martialLevelLimit,
            0, 0, 0, 0, 0,
            0, 0, 0, 0, 0,
            0, checkSetItem, 0,
            strength, dexterity, vitality, intelligent, luck,
            attackPower, defensePower, attackSuccess, attackBlock,
            elementAttackPower, elementDefensePower, critical,
            0, 0, null,
            0, 0, 0, capeInfo2, 0);
    }

    private static EquippedItemSlot Equip(int slotIndex, ItemRowDto item, byte enchant = 0, byte combine = 0,
        byte refine = 0, byte socket = 0)
    {
        return new EquippedItemSlot(slotIndex, item, enchant, combine, refine, socket);
    }

    // MaxLife = (int)(Vit*20) + LevelFactor, pet-double applied right after
    [Fact]
    public void ComputeBaseStats_NoEquipment_MaxLifeIsVitalityTimesTwentyPlusLevelFactor()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1, 50));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        // (int)(100*20) = 2000, +50 level factor, pet-double is a no-op with no pet (2050 >= 0).
        Assert.Equal(2050, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_LevelInHighBand_ClampsToLevelOneFortyFiveForFactorLookup()
    {
        // levels 146-157 clamp down to the level-145 row (not a zero factor); 150 is inside that band
        var attributes = Attributes(10, level: 150);
        var levels = Levels(LevelRow(145, 999)); // only 145 is seeded -- level 150 must resolve to it

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(1199, stats.MaxLife); // (int)(10*20)=200 + 999
    }

    [Fact]
    public void ComputeBaseStats_LevelBeyondHighBand_ContributesZeroLevelFactor()
    {
        // a level past the high band is not clamped to 145 either -- it reads as a zero level-factor contribution
        var attributes = Attributes(10, level: 200);
        var levels = Levels(LevelRow(145, 999)); // must NOT be consulted for level 200

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(200, stats.MaxLife); // (int)(10*20)=200 + 0 (zero level row), not +999
    }

    // MaxMana = (int)(Ki*15.3100004196167f) -- the exact literal, not a rounded 15.31f
    [Fact]
    public void ComputeBaseStats_NoEquipment_MaxManaUsesExactKiCoefficientPlusLevelFactor()
    {
        // Ki=50 -> 50*15.3100004196167 = 765.50002... (comfortably past the 765.5 half-boundary either way).
        var attributes = Attributes(intelligence: 50, level: 1);
        var levels = Levels(LevelRow(1, mana: 100));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(865, stats.MaxMana); // 765 + 100
    }

    [Fact]
    public void ComputeBaseStats_Unarmed_AttackPowerUsesDefaultWeaponCoefficients()
    {
        // No weapon -> fStr=2.25 (exact in binary: 2.25=9/4), fKi=1.67. Str=53 -> 53*2.25=119.25 exactly.
        var attributes = Attributes(strength: 53, level: 1);
        var levels = Levels(LevelRow(1));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(119, stats.AttackPower);
    }

    [Fact]
    public void ComputeBaseStats_AtkClassWeapon_AttackPowerUsesHigherCoefficients()
    {
        // Weapon Sort 14 ("atk" class): fStr=3.80. 53*3.80=201.4, a safe 0.4 away from the 201/202 boundary.
        var attributes = Attributes(strength: 53, level: 1);
        var levels = Levels(LevelRow(1));
        var weapon = Item(90001, 14, 50); // item Level 50 only feeds the (unrelated) IU-effect branch
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(7, weapon)];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(201, stats.AttackPower);
    }

    [Fact]
    public void ComputeBaseStats_DefClassWeapon_AttackPowerUsesDefClassCoefficients()
    {
        // Weapon Sort 13 ("def" class): fStr=3.65. 53*3.65=193.45, safe.
        var attributes = Attributes(strength: 53, level: 1);
        var levels = Levels(LevelRow(1));
        var weapon = Item(90002, 13, 50);
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(7, weapon)];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(193, stats.AttackPower);
    }

    [Fact]
    public void ComputeBaseStats_NoEquipment_DefensePowerUsesWisdomCoefficient()
    {
        // Dexterity=50 -> 50*9.63=481.5 (decimal), a safe 0.5 away from either neighboring integer.
        var attributes = Attributes(dexterity: 50, level: 1);
        var levels = Levels(LevelRow(1));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(481, stats.DefensePower);
    }

    [Fact]
    public void ComputeBaseStats_LegendaryArmor_DefensePowerAddsEnchantBonus()
    {
        // Armor Sort 1 (legendary/type-6): += Enchant*1000 (pure int math, no float ambiguity)
        var attributes = Attributes(dexterity: 50, level: 1);
        var levels = Levels(LevelRow(1));
        var armor = Item(90003, 1);
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(2, armor, 10)];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(481 + 10 * 1000, stats.DefensePower);
    }

    [Fact]
    public void ApplyPetDoubleRule_StatMeetsPet_AddsPetValue()
    {
        Assert.Equal(150, StatCalculator.ApplyPetDoubleRule(100, 50));
    }

    [Fact]
    public void ApplyPetDoubleRule_StatExactlyEqualsPet_TreatsAsMeetingIt()
    {
        Assert.Equal(100, StatCalculator.ApplyPetDoubleRule(50, 50)); // >= is inclusive
    }

    [Fact]
    public void ApplyPetDoubleRule_StatBelowPet_DoublesStatInstead()
    {
        Assert.Equal(60, StatCalculator.ApplyPetDoubleRule(30, 50));
    }

    [Fact]
    public void ComputeBaseStats_PetLifeAboveRunningTotal_DoublesMaxLifeInsteadOfAdding()
    {
        var attributes = Attributes(10, level: 1); // (int)(10*20)=200, no level factor
        var levels = Levels(LevelRow(1));
        var pet = new PetStatContribution(500); // 200 < 500 -> double

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, pet: pet);

        Assert.Equal(400, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_PetLifeAtOrBelowRunningTotal_AddsPetLifeToMaxLife()
    {
        var attributes = Attributes(100, level: 1); // (int)(100*20)=2000
        var levels = Levels(LevelRow(1));
        var pet = new PetStatContribution(100); // 2000 >= 100 -> add

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, pet: pet);

        Assert.Equal(2100, stats.MaxLife);
    }

    // NXT sets: 77000-77023, tribe-scoped, tiers 101/102/103 at 2/4/6 matched pieces
    [Fact]
    public void DetectNxtSetNumber_AllSixCanonicalSlotsMatchTribeZero_ReturnsTierOneOhThree()
    {
        IReadOnlyList<EquippedItemSlot> equipment =
        [
            Equip(0, Item(77003)), // ring
            Equip(2, Item(77004)), // armor
            Equip(3, Item(77005)), // gloves
            Equip(4, Item(77006)), // amulet
            Equip(5, Item(77007)), // boots
            Equip(7, Item(77000)) // weapon (any of the 3 tribe-0 weapons)
        ];

        Assert.Equal(103, SetBonusTables.DetectNxtSetNumber(0, equipment));
    }

    [Fact]
    public void DetectNxtSetNumber_TwoOfSixSlotsMatch_ReturnsTierOneOhOne()
    {
        IReadOnlyList<EquippedItemSlot> equipment =
        [
            Equip(0, Item(77003)), // ring
            Equip(7, Item(77000)) // weapon
        ];

        Assert.Equal(101, SetBonusTables.DetectNxtSetNumber(0, equipment));
    }

    [Fact]
    public void DetectNxtSetNumber_NoMatchingPieces_ReturnsZero()
    {
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(7, Item(99999))];

        Assert.Equal(0, SetBonusTables.DetectNxtSetNumber(0, equipment));
    }

    [Fact]
    public void ComputeBaseStats_FullNxtTribeZeroSet_AppliesFlatBonusesAcrossHpMpAtkDef()
    {
        // isolates the NXT tier-103 flat bonuses + the MY_HP "+15000 any set" rule (fires since mSetNumber=103 > 0)
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment =
        [
            Equip(0, Item(77003)),
            Equip(2, Item(77004)),
            Equip(3, Item(77005)),
            Equip(4, Item(77006)),
            Equip(5, Item(77007)),
            Equip(7, Item(77000))
        ];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(3000 + 15000, stats.MaxLife); // NXT-103 +3000, MY_HP "any set" +15000
        Assert.Equal(3000, stats.MaxMana); // NXT-103 +3000, no MY_HP-equivalent for mana
        Assert.Equal(1500, stats.AttackPower); // NXT-103 +1500
        Assert.Equal(3000, stats.DefensePower); // NXT-103 +3000
    }

    // EPET (slot 8) must be excluded from the coefSet multiplier in every per-slot stat loop, not just AttackPower

    [Fact]
    public void ComputeBaseStats_SetItemInPetSlot_AttackSuccessCountsFlatContributionButNotSetCoefficient()
    {
        // Set 5 grants a x1.0 AttackSuccess coefficient -- if the EPET guard were missing, the pet-slot item's
        // flat AttackSuccess would be doubled by the coefficient bonus.
        var attributes = Attributes(strength: 0, level: 1);
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment =
        [
            Equip(0, Item(1, 2, attackSuccess: 100)), // set-5 member elsewhere is assumed via legacySetNumber
            Equip(8, Item(2, 2, attackSuccess: 100)) // pet slot (EPET=8)
        ];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels, 5);

        // slot0: 100 flat + 100*1.0 coef = 200; slot8 (EPET): 100 flat ONLY, no coef bonus = 100. Total 300.
        Assert.Equal(300, stats.AttackSuccess);
    }

    [Fact]
    public void ComputeBaseStats_SetItemInPetSlot_LuckCountsFlatContributionButNotSetCoefficient()
    {
        // Set 20 grants a x0.10 Luck coefficient -- same EPET guard, different stat/table
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(8, Item(3, 2, luck: 100))];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels, 20);

        Assert.Equal(100, stats.Luck); // flat only -- 110 would mean the coefficient leaked onto EPET
    }

    // buffs and the ATK/DEF pet-double rule apply at the wrapper layer, not in the base cache
    [Fact]
    public void ComputeEffectiveStats_BuffPercent_AppliesToAttackPower()
    {
        var attributes = Attributes(strength: 100, level: 1); // unarmed, exact: (int)(100*2.25)=225
        var levels = Levels(LevelRow(1));
        var buffs = new BuffInfo { Buff = new int[70] };
        buffs.Buff[0] = 50; // aBuff[0][0] = +50% on AttackPower

        var stats = StatCalculator.ComputeEffectiveStats(attributes, NoEquipment, levels, buffs);

        // (int)(225 * 150 * 0.01) = 337 (comfortably clear of the 337.5 boundary either way).
        Assert.Equal(337, stats.AttackPower);
    }

    [Fact]
    public void ComputeEffectiveStats_PetDoubleForAttackPower_AppliesAtWrapperLevelNotBaseLevel()
    {
        // pet-double for ATK/DEF lives in the Get* wrappers, unlike HP/MP where it's part of the cached GetBase* value
        var attributes = Attributes(strength: 13, level: 1); // unarmed, exact: (int)(13*2.25)=29
        var levels = Levels(LevelRow(1));
        var pet = new PetStatContribution(AttackPower: 100); // 29 < 100 -> double

        var baseStats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, pet: pet);
        var effectiveStats = StatCalculator.ComputeEffectiveStats(attributes, NoEquipment, levels, pet: pet);

        Assert.Equal(29, baseStats.AttackPower); // undoubled at the base/cache layer
        Assert.Equal(58, effectiveStats.AttackPower); // doubled once the wrapper applies the pet rule
    }
}
