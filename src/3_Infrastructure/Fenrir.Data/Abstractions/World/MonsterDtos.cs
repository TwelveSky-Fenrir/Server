using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

[GenerateDto]
public sealed partial record MonsterRowDto(
    int MonsterId,
    string Name,
    string? ChatLine1,
    string? ChatLine2,
    byte Type,
    byte SpecialType,
    byte DamageType,
    short DataSortNumber,
    short Size1,
    short Size2,
    short Size3,
    short Size4,
    byte SizeCategory,
    byte CheckCollision,
    byte TotalHitNum,
    byte TotalSkillHitNum,
    short ItemLevel,
    short MartialItemLevel,
    short RealLevel,
    short MartialRealLevel,
    int GeneralExperience,
    int PatExperience,
    int Life,
    byte AttackType,
    short RadiusInfo1,
    short RadiusInfo2,
    short WalkSpeed,
    short RunSpeed,
    short DeathSpeed,
    int AttackPower,
    int DefensePower,
    int AttackSuccess,
    int AttackBlock,
    int ElementAttackPower,
    int ElementDefensePower,
    short Critical,
    short FollowInfo1,
    short FollowInfo2,
    int SummonTime1,
    int SummonTime2,
    short FrameInfo1,
    short FrameInfo2,
    short FrameInfo3,
    short FrameInfo4,
    short FrameInfo5,
    short FrameInfo6,
    short HitFrame1,
    short HitFrame2,
    short HitFrame3,
    short SkillHitFrame1,
    short SkillHitFrame2,
    short SkillHitFrame3,
    short BulletInfo1,
    short BulletInfo2);

[GenerateDto]
public sealed partial record MonsterDropMoneyRowDto(
    int MonsterId,
    int DropRate,
    int MinAmount,
    int MaxAmount);

[GenerateDto]
public sealed partial record MonsterDropPotionRowDto(
    int MonsterId,
    byte SlotIndex,
    int DropRate,
    int PotionItemId);

[GenerateDto]
public sealed partial record MonsterDropExtraItemRowDto(
    int MonsterId,
    byte SlotIndex,
    int DropRate,
    int? ItemId);

[GenerateDto]
public sealed partial record MonsterDropCategoryRateRowDto(
    int MonsterId,
    byte CategoryIndex,
    int Value);

[GenerateDto]
public sealed partial record MonsterDropQuestItemRowDto(
    int MonsterId,
    int DropRate,
    int QuestItemId);
