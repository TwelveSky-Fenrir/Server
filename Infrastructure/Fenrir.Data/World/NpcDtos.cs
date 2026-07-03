using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.World;

/// <summary>
///     One world.Npcs row -- ordinal contract of world.usp_Npc_GetAll's single result set (131 rows).
///     Constructor order must track the SELECT column order exactly (invariant I-04); [GenerateDto] maps by
///     position, not by name.
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

/// <summary>One world.NpcMenuOptions slot -- world.usp_NpcMenuOption_GetAll (SlotIndex 0-99).</summary>
[GenerateDto]
public sealed partial record NpcMenuOptionRowDto(
    int NpcId,
    short SlotIndex,
    int OptionId);

/// <summary>
///     One populated world.NpcShopItems slot -- world.usp_NpcShopItem_GetAll (ShopPage 0-2, SlotIndex 0-27).
///     ItemId is NULL for a populated slot whose legacy item half was 0.
/// </summary>
[GenerateDto]
public sealed partial record NpcShopItemRowDto(
    int NpcId,
    byte ShopPage,
    byte SlotIndex,
    int? ItemId);

/// <summary>
///     One populated world.NpcSkillOffers slot -- world.usp_NpcSkillOffer_GetAll. ArrayKind/Tier/Dim2/Dim3
///     encode which of the two legacy nSkillInfo arrays (and which dimension of it) the slot came from --
///     see world.NpcSkillOffers' table header for the mapping.
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

/// <summary>One world.NpcGambleCosts cell -- world.usp_NpcGambleCost_GetAll (GambleTier 0-144, CostIndex 0-14).</summary>
[GenerateDto]
public sealed partial record NpcGambleCostRowDto(
    int NpcId,
    short GambleTier,
    byte CostIndex,
    int Value);
