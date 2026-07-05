using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Handler silently drops the packet unless mServerNumber is 37, 119, 124, or 84.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.Shout, ExpectedSize = 94,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct ShoutRequest : IIncomingPacket<ShoutRequest>
{
    [FixedString(61)] public required string Content { get; init; }
    public required ItemLinkInfo Link { get; init; }
}
