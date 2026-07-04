using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Unlike most handlers, an out-of-range Sort is ignored rather than triggering Quit().
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PlaytimeBuff, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct PlaytimeBuffRequest : IIncomingPacket<PlaytimeBuffRequest>
{
    public required int Sort { get; init; }
}
