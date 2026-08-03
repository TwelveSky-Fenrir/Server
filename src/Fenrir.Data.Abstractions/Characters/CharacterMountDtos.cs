using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateDto]
public sealed partial record CharacterMountSlotDto(byte Slot, int ItemId, int ExpActivity, int Power);
