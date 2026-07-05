using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyLeave, ExpectedSize = 14)]
public readonly partial record struct PartyLeaveResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
