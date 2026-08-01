using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.World;

[GenerateDto]
public sealed partial record TribeSkillEquivalenceRowDto(
    byte GroupIndex,
    byte TribeId,
    int SkillId);

[GenerateDto]
public sealed partial record TribeItemEquivalenceRowDto(
    byte GroupIndex,
    byte TribeId,
    int ItemId);

[GenerateDto]
public sealed partial record TribeCostumeEquivalenceRowDto(
    byte GroupIndex,
    byte TribeId,
    int ItemId);
