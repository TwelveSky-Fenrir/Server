using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Case 120 (tier-2→3 upgrade) isn't compiled in EU33 (#ifndef LNW33); falls to default → Quit.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.MountState, ExpectedSize = 17,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct MountStateRequest : IIncomingPacket<MountStateRequest>
{
    public required int Sort { get; init; }
    public required int Value { get; init; }
}
