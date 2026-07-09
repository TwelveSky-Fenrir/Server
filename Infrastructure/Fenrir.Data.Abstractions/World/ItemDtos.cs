using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

// world.usp_Item_GetAll RS0; ctor order must match the SELECT exactly (ordinal mapping).
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
    short DataNumber3D,
    short AddDataNumber3D,
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
    short CheckDateItem,
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

/// <summary>world.usp_Item_GetAll RS1 (SlotIndex 0-7); SkillId null when the slot had no skill wired up.</summary>
[GenerateDto]
public sealed partial record ItemBonusSkillRowDto(
    int ItemId,
    byte SlotIndex,
    int? SkillId,
    short Value);
