using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.SmeltItem,
    ExpectedSize = 13)]
public readonly partial record struct SmeltItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Cost { get; init; }
    public required int Value { get; init; }
}
