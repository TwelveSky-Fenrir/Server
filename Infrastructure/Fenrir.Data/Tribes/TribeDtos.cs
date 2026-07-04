using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Tribes;

/// <summary>game.usp_Tribe_GetAll.</summary>
[GenerateDto]
public sealed partial record TribeSummaryDto(
    byte TribeId,
    int? MasterCharacterId,
    int? Points);

/// <summary>One occupied tribe sub-master slot (game.usp_TribeSubMaster_GetByTribe) -- ordinal contract.</summary>
[GenerateDto]
public sealed partial record TribeSubMasterDto(
    byte TribeId,
    byte SlotIndex,
    int CharacterId);

/// <summary>One tribe bank slot balance (game.usp_TribeBank_GetByTribe) -- ordinal contract.</summary>
[GenerateDto]
public sealed partial record TribeBankSlotDto(
    byte TribeId,
    byte SlotIndex,
    int Amount);
