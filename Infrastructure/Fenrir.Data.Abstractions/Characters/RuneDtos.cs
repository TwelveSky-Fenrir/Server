using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Characters;

/// <summary>
///     One occupied rune socket for a character -- ordinal-mapped result of <c>usp_Character_GetRunes</c>.
///     Legacy <c>aRuneSystem[SocketIndex] = RuneItemId</c>, <c>aRuneSystemStat[SocketIndex] = RuneStat</c> (the
///     packed ISUIM stat), Server/Header/Protocol/STRUCT.h:572-573. A socket with no row is empty and is
///     hydrated to (RuneItemId=0, RuneStat=0) by the caller.
/// </summary>
[GenerateDto]
public sealed partial record CharacterRuneSocketDto(
    byte SocketIndex,
    int RuneItemId,
    int RuneStat);
