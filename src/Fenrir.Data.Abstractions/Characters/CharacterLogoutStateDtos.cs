using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Characters;

public sealed record CharacterLogoutStateSnapshot(
    int CharacterId,
    int LastZone,
    int PosX,
    int PosY,
    int PosZ,
    int Life,
    int Mana);

[GenerateDto]
public sealed partial record CharacterLogoutStateDto(
    int CharacterId,
    int LastZone,
    int PosX,
    int PosY,
    int PosZ,
    int Life,
    int Mana);
