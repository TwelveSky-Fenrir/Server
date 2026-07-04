using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Same wire shape as AttackRequest; server overwrites the result fields before rebroadcasting.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Attack, ExpectedSize = 69)]
public readonly partial record struct AttackResponse : IOutgoingPacket
{
    public required AttackForProtocol AttackInfo { get; init; }
}
