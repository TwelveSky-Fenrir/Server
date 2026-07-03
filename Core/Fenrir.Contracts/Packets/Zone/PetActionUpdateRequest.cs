using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_UPDATE_PET_ACTION_SEND (CLIENT.h:506-509) — same wire layout as CZ 15/16
///     (<c>CZ_AVATAR_ACTION_SEND</c>): reuses <see cref="ActionInfo" /> (104 bytes, 26×4, no padding).
///     The handler (S04_MyWork02.cpp:15701) only reads the pet fields of the struct
///     (<c>aPetSort</c>, <c>aPetFront</c>, <c>aPetLocation[3]</c>, <c>aPetTargetLocation[3]</c>, copied
///     into <c>mDATA.aAction</c>); the other 18 fields are ignored but still part of the wire. No direct
///     response: the pet's position is rebroadcast passively via the avatar action channel (ZC 15 /
///     <c>OBJECT_FOR_AVATAR</c>). The dedicated ZC 197 (<c>ZC_PET_MOVE_RECV</c>) is never emitted (see
///     the "dead packets" section).
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PetActionUpdate,
    ExpectedSize = 113, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct PetActionUpdateRequest : IIncomingPacket<PetActionUpdateRequest>
{
    public required ActionInfo Action { get; init; }
}
