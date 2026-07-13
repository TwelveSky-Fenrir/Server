using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneHandshake, ExpectedSize = 5)]
public readonly partial record struct ZoneHandshakeResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
