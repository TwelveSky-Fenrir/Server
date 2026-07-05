using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Sort must be strictly 0 or 1, else Quit().
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.CostumeVisibility,
    ExpectedSize = 13, AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct CostumeVisibilityRequest : IIncomingPacket<CostumeVisibilityRequest>
{
    public required int Sort { get; init; }
}
