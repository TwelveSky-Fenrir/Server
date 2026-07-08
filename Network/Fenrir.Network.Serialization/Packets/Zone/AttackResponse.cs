using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Same wire shape as AttackRequest; server overwrites the result fields before rebroadcasting.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Attack, ExpectedSize = 69)]
public readonly record struct AttackResponse : IOutgoingPacket
{
    public required AttackForProtocol AttackInfo { get; init; }
}
