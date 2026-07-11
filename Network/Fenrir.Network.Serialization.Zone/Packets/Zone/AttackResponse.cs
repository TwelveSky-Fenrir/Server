using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Attack, ExpectedSize = 69)]
public readonly partial record struct AttackResponse : IOutgoingPacket
{
    public required AttackForProtocol AttackInfo { get; init; }
}
