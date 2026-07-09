using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

// Semantics live in Sort (e.g. 7=duel, 10=pet), not the wire shape; distinct from the self-state packet (different layout).
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.AvatarStateFlag, ExpectedSize = 25)]
public readonly record struct AvatarStateFlagResponse : IOutgoingPacket
{
    public required int ServerIndex { get; init; }
    public required uint UniqueNumber { get; init; }
    public required int Sort { get; init; }
    public required int Value01 { get; init; }
    public required int Value02 { get; init; }
    public required int Value03 { get; init; }
}
