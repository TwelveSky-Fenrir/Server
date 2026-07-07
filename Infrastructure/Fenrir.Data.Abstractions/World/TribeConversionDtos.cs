using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

// world.usp_TribeConversionCatalog_GetAll RS0; ordinal-mapped, ctor order must match the SELECT.
// GroupIndex ties the 3 TribeId (0=Noble Dragon,1=Royal Serpent,2=Grand Tiger) rows of one legacy tsSkill
// array row together -- see Tables/world/TribeSkillEquivalences.sql for the full citation/rationale.
[GenerateDto]
public sealed partial record TribeSkillEquivalenceRowDto(
    byte GroupIndex,
    byte TribeId,
    int SkillId);

/// <summary>
///     world.usp_TribeConversionCatalog_GetAll RS1 -- equipped-gear equivalence (Server/ts25zone/S04_MyWork03.cpp's
///     tsItem table). The reserved 87129-87257 "V2" id band is NOT represented as separate rows here; a
///     consumer must apply the same +129 offset arithmetic the seeding procedure itself uses -- see
///     Tables/world/TribeItemEquivalences.sql's own remarks.
/// </summary>
[GenerateDto]
public sealed partial record TribeItemEquivalenceRowDto(
    byte GroupIndex,
    byte TribeId,
    int ItemId);

/// <summary>
///     world.usp_TribeConversionCatalog_GetAll RS2 -- worn-costume equivalence (Server/ts25zone/S04_MyWork03.cpp's
///     tsFation table). Fenrir has no durable per-character costume-wardrobe storage yet -- see
///     Tables/world/TribeCostumeEquivalences.sql's own remarks; this catalog only supplies the lookup data.
/// </summary>
[GenerateDto]
public sealed partial record TribeCostumeEquivalenceRowDto(
    byte GroupIndex,
    byte TribeId,
    int ItemId);
