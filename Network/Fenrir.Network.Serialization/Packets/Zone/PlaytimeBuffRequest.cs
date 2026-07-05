using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Unlike most handlers, an out-of-range Sort is ignored rather than triggering Quit().
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.PlaytimeBuff, ExpectedSize = 13,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct PlaytimeBuffRequest : IIncomingPacket<PlaytimeBuffRequest>
{
    public required int Sort { get; init; }
}
