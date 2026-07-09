using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Handler reads only the pet fields of ActionInfo (rest ignored); no reply - position rebroadcasts via ZC 15.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PetActionUpdate,
    ExpectedSize = 113, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct PetActionUpdateRequest : IIncomingPacket<PetActionUpdateRequest>
{
    public required ActionInfo Action { get; init; }
}
