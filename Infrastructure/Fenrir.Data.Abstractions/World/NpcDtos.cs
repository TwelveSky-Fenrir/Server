using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

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

[GenerateDto]
public sealed partial record NpcMenuOptionRowDto(
    int NpcId,
    short SlotIndex,
    int OptionId);

[GenerateDto]
public sealed partial record NpcShopItemRowDto(
    int NpcId,
    byte ShopPage,
    byte SlotIndex,
    int? ItemId);

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

[GenerateDto]
public sealed partial record NpcSpeechRowDto(
    int NpcId,
    byte SpeechGroup,
    byte SpeechIndex,
    string Text);

[GenerateDto]
public sealed partial record NpcGambleCostRowDto(
    int NpcId,
    short GambleTier,
    byte CostIndex,
    int Value);
