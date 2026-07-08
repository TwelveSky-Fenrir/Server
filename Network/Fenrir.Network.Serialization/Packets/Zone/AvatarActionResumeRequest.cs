using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AvatarActionResume,
    ExpectedSize = 113, AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly partial record struct AvatarActionResumeRequest : IIncomingPacket<AvatarActionResumeRequest>
{
    public required ActionInfo Action { get; init; }
}
