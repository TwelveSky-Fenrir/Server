using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Sort must be 0/1 (else Quit()). AutoHunt payload is copied verbatim with zero validation server-side — anti-cheat gap to cover.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.AutoHuntToggle, ExpectedSize = 125,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct AutoHuntToggleRequest : IIncomingPacket<AutoHuntToggleRequest>
{
    public required int Sort { get; init; }
    public required AutoHunt AutoHunt { get; init; }
}
