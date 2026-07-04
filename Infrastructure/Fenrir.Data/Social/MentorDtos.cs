using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Social;

/// <summary>A character's own teacher/student bond (game.usp_CharacterMentor_GetForCharacter) -- ordinal contract: TeacherCharacterId, TeacherName, StudentCharacterId, StudentName (either/both pairs may be null -- no bond on that side).</summary>
[GenerateDto]
public sealed partial record CharacterMentorDto(
    int? TeacherCharacterId,
    string? TeacherName,
    int? StudentCharacterId,
    string? StudentName);
