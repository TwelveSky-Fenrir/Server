using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     CZ_ANIMAL_ABSORB_SEND (CLIENT.h:345-348, typedef shared with CZ_GET_CASH_SIZE_SEND/
///     CZ_PAT_ACTION_SEND (115, dead)/CZ_TIME_EFFECT_SEND/CZ_RANK_BUFF_SEND/CZ_MISSION_COMPLETE_SEND) —
///     toggle mount absorption (S04_MyWork02.cpp:13101). <c>Sort</c> 1=activate (requires a mounted
///     animal and <c>aAnimalAbsorbTime ≥ 1</c>, else Quit), 2=deactivate. Other → Quit. No dedicated
///     response: emits <c>AVATAR_CHANGE_INFO_1</c> (sort 26) broadcast + <c>AVATAR_CHANGE_INFO_2</c>
///     (S079MOUNT_ABSORB_STATE), both outside this lot. ZC 102 is NOT emitted by this handler.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MountAbsorb, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct MountAbsorbRequest : IIncomingPacket<MountAbsorbRequest>
{
    public required int Sort { get; init; }
}
