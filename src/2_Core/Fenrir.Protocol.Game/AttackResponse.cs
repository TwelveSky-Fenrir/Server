using Fenrir.Core.Attributes;
using Fenrir.Core.Packets.Shared;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Attack, ExpectedSize = 69)]
public readonly partial record struct AttackResponse : IOutgoingPacket
{
    public required AttackForProtocol AttackInfo { get; init; }
}
