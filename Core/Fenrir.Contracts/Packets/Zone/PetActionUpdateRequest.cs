using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Handler reads only the pet fields of ActionInfo (rest ignored); no reply - position rebroadcasts via ZC 15.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PetActionUpdate,
    ExpectedSize = 113, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct PetActionUpdateRequest : IIncomingPacket<PetActionUpdateRequest>
{
    public required ActionInfo Action { get; init; }
}
