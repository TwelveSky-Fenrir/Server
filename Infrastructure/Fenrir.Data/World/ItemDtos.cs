using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

/// <summary>
///     One world.Items row -- ordinal contract of world.usp_Item_GetAll's RS0 (the legacy ITEM_INFO catalog,
///     34,353 rows). Constructor order must track the SELECT column order exactly (invariant I-04);
///     [GenerateDto] maps by position, not by name.
/// </summary>
[GenerateDto]
public sealed partial record ItemRowDto(
    int ItemId,
    string Name,
    string? Description1,
    string? Description2,
    string? Description3,
    byte Type,
    byte Sort,
    short DataNumber2D,
    byte DataNumber3D,
    byte AddDataNumber3D,
    short Level,
    byte MartialLevel,
    byte EquipInfo1,
    byte EquipInfo2,
    int BuyCost,
    int SellCost,
    int BuyCost2,
    short LevelLimit,
    byte MartialLevelLimit,
    byte CheckMonsterDrop,
    byte CheckNpcSell,
    byte CheckNpcShop,
    byte CheckAvatarDrop,
    byte CheckAvatarTrade,
    byte CheckAvatarShop,
    byte CheckImprove,
    byte CheckHighImprove,
    byte CheckHighItem,
    byte CheckLowItem,
    byte CheckExchange,
    byte CheckSetItem,
    byte CheckDateItem,
    short Strength,
    short Dexterity,
    short Vitality,
    short Intelligent,
    short Luck,
    short AttackPower,
    short DefensePower,
    short AttackSuccess,
    short AttackBlock,
    short ElementAttackPower,
    short ElementDefensePower,
    byte Critical,
    short PotionType1,
    short PotionType2,
    int? GainSkillNumber,
    short LastAttackBonusInfo1,
    short LastAttackBonusInfo2,
    byte CapeInfo1,
    byte CapeInfo2,
    byte CapeInfo3);

/// <summary>
///     One populated world.ItemBonusSkills slot -- ordinal contract of world.usp_Item_GetAll's RS1
///     (ItemId, SlotIndex 0-7, SkillId, Value). SkillId is NULL when the legacy slot carried a value with
///     no skill wired up.
/// </summary>
[GenerateDto]
public sealed partial record ItemBonusSkillRowDto(
    int ItemId,
    byte SlotIndex,
    int? SkillId,
    short Value);
