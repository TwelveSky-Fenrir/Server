using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

// world.usp_Level_GetAll; ordinal-mapped, ctor order must match the SELECT.
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
