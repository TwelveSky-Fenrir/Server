using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Login;

/// <summary>
///     CL_CREATE_AVATAR_SEND2, opcode 17. <see cref="AvatarPost" /> is the target character slot (0-2, no
///     named constant exists in the legacy source either). <see cref="PreviousTribe" /> independently selects
///     which of the three starter-kit racial templates (Noble Dragon/Royal Serpent/Grand Tiger) to grant --
///     legacy never cross-checks it against <see cref="Tribe" /> at creation time, nor does legacy itself
///     reject a value outside 0/1/2 (the per-template equipment/skill/hotkey switch simply grants nothing for
///     any other value). Fenrir's own CreateAvatarHandler nonetheless range-checks <see cref="PreviousTribe" />
///     to 0-2 before ever calling the creation service: game.Characters.PreviousTribe carries a
///     CK_Characters_PreviousTribe CHECK constraint enforcing that same range
///     (Migrations/018_character_previous_tribe_and_mount_readpath.sql:35-36), so an out-of-range value that
///     reached the database would fail there instead, as an uncaught, uncoded constraint violation --
///     rejecting it here up front, with the same Malformed-disconnect treatment as every other
///     structurally-invalid field on this request, is a deliberate Fenrir-side divergence from legacy's own
///     permissiveness, not a missed parity citation.
///     <see cref="Gender" /> is read and persisted but never range-checked nor referenced by any grant.
/// </summary>
/// <remarks>
///     Réf. C++ : Server/ts25login/S04_MyWork02.cpp:582-751 (full precondition sequence: slot/name, tribe
///     range, fourth-faction exclusion, head/face range, and the point Tribe/PreviousTribe are persisted with
///     no cross-check between them) ; Server/ts25login/S04_MyWork02.cpp:753-838 (the PreviousTribe switch:
///     per-race weapon-code validation against exactly three legal codes, for values 0/1/2 only) ;
///     Server/Header/Protocol/DEFINE.h:280 (13-character fixed name field) ;
///     Migrations/018_character_previous_tribe_and_mount_readpath.sql:35-36 (CK_Characters_PreviousTribe, the
///     DB-level constraint that motivates this handler's own PreviousTribe range check -- see
///     CreateAvatarHandler's remarks for the full citation).
/// </remarks>
[FenrirPacket(FenrirServer.Login, FenrirDirection.Incoming, Opcodes.Login.Incoming.CreateAvatar,
    ExpectedSize = 50, AllowedStates = [(byte)LoginSessionState.Authenticated, (byte)LoginSessionState.CharSelect])]
public readonly partial record struct CreateAvatarRequest : IIncomingPacket<CreateAvatarRequest>
{
    public required int AvatarPost { get; init; }
    public required int Tribe { get; init; }
    public required int PreviousTribe { get; init; }
    public required int Gender { get; init; }
    public required int Head { get; init; }
    public required int Face { get; init; }
    public required int Weapon { get; init; }
    [FixedString(13)] public required string AvatarName { get; init; }
}
