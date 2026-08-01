using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Social;

[GenerateDto]
public sealed partial record CharacterMentorDto(
    int? TeacherCharacterId,
    string? TeacherName,
    int? StudentCharacterId,
    string? StudentName);
