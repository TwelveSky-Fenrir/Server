using System.Collections.Frozen;
using Fenrir.Application.Game.Stats;
using Fenrir.Data.Abstractions.World;
using Fenrir.Network.Serialization.Shared.Packets.Shared;

namespace Fenrir.Application.Game.Tests.Stats;

public class StatCalculatorTests
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
        byte capeInfo2 = 0,
        byte type = 0,
        byte equipInfo2 = 0)
    {
        return new ItemRowDto(
            itemId, $"Item{itemId}", null, null, null,
            type, sort, 0, 0, 0,
            level, 0, 0, equipInfo2,
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

    [Fact]
    public void ComputeBaseStats_NoEquipment_MaxLifeIsVitalityTimesTwentyPlusLevelFactor()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1, 50));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(2050, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_LevelInHighBand_ClampsToLevelOneFortyFiveForFactorLookup()
    {
        var attributes = Attributes(10, level: 150);
        var levels = Levels(LevelRow(145, 999));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(1199, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_LevelBeyondHighBand_ContributesZeroLevelFactor()
    {
        var attributes = Attributes(10, level: 200);
        var levels = Levels(LevelRow(145, 999));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(200, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_NoEquipment_MaxManaUsesExactKiCoefficientPlusLevelFactor()
    {
        var attributes = Attributes(intelligence: 50, level: 1);
        var levels = Levels(LevelRow(1, mana: 100));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(865, stats.MaxMana);
    }

    [Fact]
    public void ComputeBaseStats_Unarmed_AttackPowerUsesDefaultWeaponCoefficients()
    {
        var attributes = Attributes(strength: 53, level: 1);
        var levels = Levels(LevelRow(1));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(119, stats.AttackPower);
    }

    [Fact]
    public void ComputeBaseStats_AtkClassWeapon_AttackPowerUsesHigherCoefficients()
    {
        var attributes = Attributes(strength: 53, level: 1);
        var levels = Levels(LevelRow(1));
        var weapon = Item(90001, 14, 50);
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(7, weapon)];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(201, stats.AttackPower);
    }

    [Fact]
    public void ComputeBaseStats_DefClassWeapon_AttackPowerUsesDefClassCoefficients()
    {
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
        var attributes = Attributes(dexterity: 50, level: 1);
        var levels = Levels(LevelRow(1));

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);

        Assert.Equal(481, stats.DefensePower);
    }

    [Fact]
    public void ComputeBaseStats_LegendaryArmor_DefensePowerAddsEnchantBonus()
    {
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
        Assert.Equal(100, StatCalculator.ApplyPetDoubleRule(50, 50));
    }

    [Fact]
    public void ApplyPetDoubleRule_StatBelowPet_DoublesStatInstead()
    {
        Assert.Equal(60, StatCalculator.ApplyPetDoubleRule(30, 50));
    }

    [Fact]
    public void ComputeBaseStats_PetLifeAboveRunningTotal_DoublesMaxLifeInsteadOfAdding()
    {
        var attributes = Attributes(10, level: 1);
        var levels = Levels(LevelRow(1));
        var pet = new PetStatContribution(500);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, pet: pet);

        Assert.Equal(400, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_PetLifeAtOrBelowRunningTotal_AddsPetLifeToMaxLife()
    {
        var attributes = Attributes(100, level: 1);
        var levels = Levels(LevelRow(1));
        var pet = new PetStatContribution(100);

        var stats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, pet: pet);

        Assert.Equal(2100, stats.MaxLife);
    }

    [Fact]
    public void DetectNxtSetNumber_AllSixCanonicalSlotsMatchTribeZero_ReturnsTierOneOhThree()
    {
        IReadOnlyList<EquippedItemSlot> equipment =
        [
            Equip(0, Item(77003)),
            Equip(2, Item(77004)),
            Equip(3, Item(77005)),
            Equip(4, Item(77006)),
            Equip(5, Item(77007)),
            Equip(7, Item(77000))
        ];

        Assert.Equal(103, SetBonusTables.DetectNxtSetNumber(0, equipment));
    }

    [Fact]
    public void DetectNxtSetNumber_TwoOfSixSlotsMatch_ReturnsTierOneOhOne()
    {
        IReadOnlyList<EquippedItemSlot> equipment =
        [
            Equip(0, Item(77003)),
            Equip(7, Item(77000))
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

        Assert.Equal(3000 + 15000, stats.MaxLife);
        Assert.Equal(3000, stats.MaxMana);
        Assert.Equal(1500, stats.AttackPower);
        Assert.Equal(3000, stats.DefensePower);
    }

    [Fact]
    public void ComputeBaseStats_G12FullSetForOriginTribe_GrantsBonusByPreviousTribeEvenForFourthFactionTribe()
    {
        var attributes = Attributes(level: 1, tribe: 3, previousTribe: 0);
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment =
        [
            Equip(0, Item(84500), combine: 12),
            Equip(2, Item(84501), combine: 12),
            Equip(3, Item(84502), combine: 12),
            Equip(4, Item(84503), combine: 12),
            Equip(5, Item(84504), combine: 12),
            Equip(7, Item(84505), combine: 12)
        ];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(15000, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_G12FullSetButUnrecognizedPreviousTribe_GrantsNoBonusDespiteMatchingTribeZeroIds()
    {
        var attributes = Attributes(level: 1, tribe: 0, previousTribe: 3);
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment =
        [
            Equip(0, Item(84500), combine: 12),
            Equip(2, Item(84501), combine: 12),
            Equip(3, Item(84502), combine: 12),
            Equip(4, Item(84503), combine: 12),
            Equip(5, Item(84504), combine: 12),
            Equip(7, Item(84505), combine: 12)
        ];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(0, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_NxtSetForOriginTribe_DetectsTierByPreviousTribeEvenForFourthFactionTribe()
    {
        var attributes = Attributes(level: 1, tribe: 3, previousTribe: 0);
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

        Assert.Equal(3000 + 15000, stats.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_NxtSetButUnrecognizedPreviousTribe_DetectsNoTierDespiteMatchingTribeZeroIds()
    {
        var attributes = Attributes(level: 1, tribe: 0, previousTribe: 3);
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

        Assert.Equal(0, stats.MaxLife);
    }


    [Fact]
    public void ComputeBaseStats_SetItemInPetSlot_AttackSuccessCountsFlatContributionButNotSetCoefficient()
    {
        var attributes = Attributes(strength: 0, level: 1);
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment =
        [
            Equip(0, Item(1, 2, attackSuccess: 100)),
            Equip(8, Item(2, 2, attackSuccess: 100))
        ];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels, 5);

        Assert.Equal(300, stats.AttackSuccess);
    }

    [Fact]
    public void ComputeBaseStats_SetItemInPetSlot_LuckCountsFlatContributionButNotSetCoefficient()
    {
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(8, Item(3, 2, luck: 100))];

        var stats = StatCalculator.ComputeBaseStats(attributes, equipment, levels, 20);

        Assert.Equal(100, stats.Luck);
    }

    [Fact]
    public void ComputeEffectiveStats_BuffPercent_AppliesToAttackPower()
    {
        var attributes = Attributes(strength: 100, level: 1);
        var levels = Levels(LevelRow(1));
        var buffs = new BuffInfo { Buff = new int[70] };
        buffs.Buff[0] = 50;

        var stats = StatCalculator.ComputeEffectiveStats(attributes, NoEquipment, levels, buffs);

        Assert.Equal(337, stats.AttackPower);
    }

    [Fact]
    public void ComputeEffectiveStats_PetDoubleForAttackPower_AppliesAtWrapperLevelNotBaseLevel()
    {
        var attributes = Attributes(strength: 13, level: 1);
        var levels = Levels(LevelRow(1));
        var pet = new PetStatContribution(AttackPower: 100);

        var baseStats = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels, pet: pet);
        var effectiveStats = StatCalculator.ComputeEffectiveStats(attributes, NoEquipment, levels, pet: pet);

        Assert.Equal(29, baseStats.AttackPower);
        Assert.Equal(58, effectiveStats.AttackPower);
    }


    [Fact]
    public void ComputeBaseStats_CapeEffectSort2_AddsDefenseRampContribution()
    {
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        var cape = Item(90010, 8, 100);
        IReadOnlyList<EquippedItemSlot> withoutIu = [Equip(1, cape)];
        IReadOnlyList<EquippedItemSlot> withIu = [Equip(1, cape, combine: 5)];

        var without = StatCalculator.ComputeBaseStats(attributes, withoutIu, levels);
        var with = StatCalculator.ComputeBaseStats(attributes, withIu, levels);

        Assert.Equal(10, with.DefensePower - without.DefensePower);
    }

    [Fact]
    public void ComputeBaseStats_WeaponEffectSort3_AddsHitRampContribution()
    {
        var attributes = Attributes(strength: 0, level: 1);
        var levels = Levels(LevelRow(1));
        var weapon = Item(90011, 14, 100);
        IReadOnlyList<EquippedItemSlot> withoutIu = [Equip(7, weapon)];
        IReadOnlyList<EquippedItemSlot> withIu = [Equip(7, weapon, combine: 5)];

        var without = StatCalculator.ComputeBaseStats(attributes, withoutIu, levels);
        var with = StatCalculator.ComputeBaseStats(attributes, withIu, levels);

        Assert.Equal(35, with.AttackSuccess - without.AttackSuccess);
    }

    [Fact]
    public void ComputeBaseStats_DecorationSlot_OnlyIsOctetContributesToMaxLife()
    {
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        var deco = Item(90012, type: 5, equipInfo2: 11);
        IReadOnlyList<EquippedItemSlot> withDeco = [Equip(9, deco, 45, 99)];

        var without = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);
        var with = StatCalculator.ComputeBaseStats(attributes, withDeco, levels);

        Assert.Equal(500, with.MaxLife - without.MaxLife);
    }

    [Fact]
    public void ComputeBaseStats_DecorationSlot_WrongEquipInfoCategory_ContributesNothing()
    {
        var attributes = Attributes(level: 1);
        var levels = Levels(LevelRow(1));
        var wrongCategory = Item(90013, type: 5, equipInfo2: 10);
        IReadOnlyList<EquippedItemSlot> equipment = [Equip(9, wrongCategory, 45)];

        var without = StatCalculator.ComputeBaseStats(attributes, NoEquipment, levels);
        var with = StatCalculator.ComputeBaseStats(attributes, equipment, levels);

        Assert.Equal(without.MaxLife, with.MaxLife);
    }
}
