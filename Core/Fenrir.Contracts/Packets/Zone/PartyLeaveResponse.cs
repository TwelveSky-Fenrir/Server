using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.PartyLeave, ExpectedSize = 14)]
public readonly partial record struct PartyLeaveResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
