using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateDto]
public sealed partial record CharacterStellarCoreSlotDto(byte Slot, int ItemId);
