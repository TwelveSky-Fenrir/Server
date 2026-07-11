using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Tribes;

[GenerateDto]
public sealed partial record TribeSummaryDto(
    byte TribeId,
    int? MasterCharacterId,
    int? Points);

[GenerateDto]
public sealed partial record TribeSubMasterDto(
    byte TribeId,
    byte SlotIndex,
    int CharacterId);

[GenerateDto]
public sealed partial record TribeBankSlotDto(
    byte TribeId,
    byte SlotIndex,
    int Amount);
