using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PetActionUpdate,
    ExpectedSize = 113, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct PetActionUpdateRequest : IIncomingPacket<PetActionUpdateRequest>
{
    public required ActionInfo Action { get; init; }
}
