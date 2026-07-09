using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

/// <summary>
///     world.usp_Npc_GetAll; ordinal-mapped, ctor order must match the SELECT. Tribe (1-5), Type (1-17),
///     DataSortNumber2D/3D (1-10000 each) and Size1/2/3 (1-1000 each) are all storage-level bounded (
///     CK_Npcs_Tribe, CK_Npcs_Type, CK_Npcs_DataSortNumber2D/3D, CK_Npcs_Size1/2/3 --
///     Database/Migrations/033_npc_static_data_range_checks.sql) mirroring legacy's load-time
///     <c>Npc_CheckValidElement</c> (<c>Server/Header/S15_MyShare.cpp:1836-1963</c>); no row read back through
///     this DTO can fall outside those ranges, so consuming code (<see cref="WorldDataCacheBuilder" /> and
///     downstream Domain readers) has no need to re-validate them.
/// </summary>
[GenerateDto]
public sealed partial record NpcRowDto(
    int NpcId,
    string Name,
    byte Tribe,
    byte Type,
    int DataSortNumber2D,
    int DataSortNumber3D,
    int Size1,
    int Size2,
    int Size3);

/// <summary>
///     One world.NpcMenuOptions slot -- world.usp_NpcMenuOption_GetAll (SlotIndex 0-99). OptionId is
///     storage-level bounded to exactly 1 or 2 (CK_NpcMenuOptions_OptionId --
///     Database/Migrations/033_npc_static_data_range_checks.sql; 0 is explicitly invalid per legacy's
///     <c>Npc_CheckValidElement</c>, <c>Server/Header/S15_MyShare.cpp:1910-1916</c>) -- 2 means "this NPC
///     advertises the function at <see cref="SlotIndex" />", matching <see cref="NpcFunctionGate" />'s
///     <c>OptionId == 2</c> check.
/// </summary>
[GenerateDto]
public sealed partial record NpcMenuOptionRowDto(
    int NpcId,
    short SlotIndex,
    int OptionId);

/// <summary>world.usp_NpcShopItem_GetAll (ShopPage 0-2, SlotIndex 0-27); ItemId null when the slot had no item.</summary>
[GenerateDto]
public sealed partial record NpcShopItemRowDto(
    int NpcId,
    byte ShopPage,
    byte SlotIndex,
    int? ItemId);

/// <summary>
///     world.usp_NpcSkillOffer_GetAll; ArrayKind/Tier/Dim2/Dim3 encode which legacy nSkillInfo array/dimension the
///     slot came from.
/// </summary>
[GenerateDto]
public sealed partial record NpcSkillOfferRowDto(
    int NpcSkillOfferId,
    int NpcId,
    byte ArrayKind,
    byte Tier,
    byte? Dim2,
    byte? Dim3,
    byte SlotIndex,
    int? SkillId);

/// <summary>One populated world.NpcSpeeches line -- world.usp_NpcSpeech_GetAll (SpeechGroup/SpeechIndex 0-4).</summary>
[GenerateDto]
public sealed partial record NpcSpeechRowDto(
    int NpcId,
    byte SpeechGroup,
    byte SpeechIndex,
    string Text);

/// <summary>
///     One world.NpcGambleCosts cell -- world.usp_NpcGambleCost_GetAll (GambleTier 0-144, CostIndex 0-14).
///     Value is storage-level bounded 0-100,000,000 (CK_NpcGambleCosts_Value --
///     Database/Migrations/033_npc_static_data_range_checks.sql), mirroring legacy's load-time
///     <c>Npc_CheckValidElement</c> (<c>Server/Header/S15_MyShare.cpp:1953-1962</c>).
/// </summary>
[GenerateDto]
public sealed partial record NpcGambleCostRowDto(
    int NpcId,
    short GambleTier,
    byte CostIndex,
    int Value);
