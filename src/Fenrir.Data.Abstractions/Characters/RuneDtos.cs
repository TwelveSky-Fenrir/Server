using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Characters;

[GenerateDto]
public sealed partial record CharacterRuneSocketDto(
    byte SocketIndex,
    int RuneItemId,
    int RuneStat);
