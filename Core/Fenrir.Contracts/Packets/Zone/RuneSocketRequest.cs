using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Item-slot mapping is strict: 93514-93517 map to slots 0-3; any mismatch disconnects.
// Field order differs from the ZC 199 response - do not reuse this layout there.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Incoming, Opcodes.Zone.Incoming.RuneSocket, ExpectedSize = 29,
    AllowedStates = [(byte)ZoneSessionState.InWorld])]
public readonly partial record struct RuneSocketRequest : IIncomingPacket<RuneSocketRequest>
{
    public required int Sort { get; init; }

    public required int RuneIndex { get; init; }

    public required int ItemIndex { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }
}
