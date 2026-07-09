using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;
using Fenrir.Network.Serialization.Zone.Wire;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Item-slot mapping is strict: 93514-93517 map to slots 0-3; any mismatch disconnects.
// Field order differs from the ZC 199 response - do not reuse this layout there.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.RuneSocket, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.Registering, (byte)ZoneSessionState.InWorld])]
public readonly record struct RuneSocketRequest : IIncomingPacket<RuneSocketRequest>
{
    public required int Sort { get; init; }

    public required int RuneIndex { get; init; }

    public required int ItemIndex { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }
}
