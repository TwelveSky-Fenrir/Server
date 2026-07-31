using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DuelEnd, ExpectedSize = 5)]
public readonly partial record struct DuelEndResponse : IOutgoingPacket
{
    public required int Result { get; init; }
}
