using CaeriusNet.Attributes.Dto;

namespace Fenrir.Data.Abstractions.Characters;

/// <summary>
///     One character's captured "logout info" snapshot -- Fenrir's stand-in for the legacy avatar record's
///     UPDATE_LOGOUT_INFO[6] array (element 0 = zone/server number, 1-3 = position, 4 = life, 5 = mana --
///     Server/Header/Protocol/DEFINE.h:368,750-757). Written by
///     <see cref="ICharacterLogoutStateRepository.UpsertAsync" /> at world entry and read back (per account)
///     by <see cref="ICharacterLogoutStateRepository.GetByAccountAsync" /> to populate the login
///     character-select train's <c>LogoutInfo</c> wire array.
/// </summary>
/// <remarks>
///     <see cref="LastZone" /> is the map/zone number the character was last registered into; position is
///     three whole numbers (the legacy capture truncates the floating-point action location). No range is
///     enforced -- the legacy tribe-vs-last-zone consistency correction and the life/mana low-word floor are
///     deferred read-side concerns whose exact inputs the D1 behavior contract never established (see the
///     <c>usp_CharacterLogoutState_GetByAccount</c> procedure's own header).
/// </remarks>
public sealed record CharacterLogoutStateSnapshot(
    int CharacterId,
    int LastZone,
    int PosX,
    int PosY,
    int PosZ,
    int Life,
    int Mana);

// Ordinal-mapped: ctor order must match usp_CharacterLogoutState_GetByAccount's SELECT order.
[GenerateDto]
public sealed partial record CharacterLogoutStateDto(
    int CharacterId,
    int LastZone,
    int PosX,
    int PosY,
    int PosZ,
    int Life,
    int Mana);
