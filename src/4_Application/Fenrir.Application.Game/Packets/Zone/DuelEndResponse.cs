using Fenrir.Core.Wire;
using Fenrir.Core.Attributes;

namespace Fenrir.Application.Game.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelEnd, ExpectedSize = 5)]
public readonly partial record struct DuelEndResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
