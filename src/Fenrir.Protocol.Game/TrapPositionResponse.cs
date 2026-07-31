using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TrapPosition,
    ExpectedSize = 9)]
public readonly partial record struct TrapPositionResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Value { get; init; }
}
