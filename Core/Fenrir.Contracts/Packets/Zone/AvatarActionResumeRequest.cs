using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AvatarActionResume,
    ExpectedSize = 113, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct AvatarActionResumeRequest : IIncomingPacket<AvatarActionResumeRequest>
{
    public required ActionInfo Action { get; init; }
}
