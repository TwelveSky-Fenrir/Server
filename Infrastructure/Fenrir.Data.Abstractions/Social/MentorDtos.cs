using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Social;

/// <summary>game.usp_CharacterMentor_GetForCharacter; either/both pairs may be null (no bond on that side).</summary>
[GenerateDto]
public sealed partial record CharacterMentorDto(
    int? TeacherCharacterId,
    string? TeacherName,
    int? StudentCharacterId,
    string? StudentName);
