using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

/// <summary>
///     One world.Monsters row joined 1:1 with world.MonsterAnimationFrames -- ordinal contract of
///     world.usp_Monster_GetAll's single result set (1,139 rows: 40 monster columns then 14 frame columns).
///     Constructor order must track the SELECT column order exactly (invariant I-04); [GenerateDto] maps by
///     position, not by name.
/// </summary>
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

/// <summary>world.MonsterDropMoney row -- world.usp_Monster_GetDrops RS0 (dense, at most 1 per monster).</summary>
[GenerateDto]
public sealed partial record MonsterDropMoneyRowDto(
    int MonsterId,
    int DropRate,
    int MinAmount,
    int MaxAmount);

/// <summary>world.MonsterDropPotions row -- world.usp_Monster_GetDrops RS1 (populated slots only, SlotIndex 0-4).</summary>
[GenerateDto]
public sealed partial record MonsterDropPotionRowDto(
    int MonsterId,
    byte SlotIndex,
    int DropRate,
    int PotionItemId);

/// <summary>
///     world.MonsterDropExtraItems row -- world.usp_Monster_GetDrops RS2 (populated slots only, SlotIndex 0-49).
///     ItemId is NULL when the legacy slot's item half was 0 (rate set, no item wired up).
/// </summary>
[GenerateDto]
public sealed partial record MonsterDropExtraItemRowDto(
    int MonsterId,
    byte SlotIndex,
    int DropRate,
    int? ItemId);

/// <summary>world.MonsterDropCategoryRates row -- world.usp_Monster_GetDrops RS3 (CategoryIndex 0-11).</summary>
[GenerateDto]
public sealed partial record MonsterDropCategoryRateRowDto(
    int MonsterId,
    byte CategoryIndex,
    int Value);

/// <summary>world.MonsterDropQuestItems row -- world.usp_Monster_GetDrops RS4 (at most 1 per monster).</summary>
[GenerateDto]
public sealed partial record MonsterDropQuestItemRowDto(
    int MonsterId,
    int DropRate,
    int QuestItemId);
