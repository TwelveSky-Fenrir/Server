using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

/// <summary>
///     One world.Levels row -- ordinal contract of world.usp_Level_GetAll (145 rows, the legacy per-level
///     progression curve). Constructor order must track the SELECT column order exactly (invariant I-04);
///     [GenerateDto] maps by position, not by name.
/// </summary>
[GenerateDto]
public sealed partial record LevelRowDto(
    short Level,
    int ExpRangeMin,
    int ExpRangeMax,
    byte RangeInfo3,
    short AttackPower,
    short DefensePower,
    short AttackSuccess,
    short AttackBlock,
    short ElementAttack,
    int Life,
    int Mana);
