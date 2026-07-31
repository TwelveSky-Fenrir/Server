using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.UseInventoryItem,
    ExpectedSize = 21)]
public readonly partial record struct UseInventoryItemResponse : IOutgoingPacket
{
    public required int Result { get; init; }

    public required int Page { get; init; }

    public required int Index { get; init; }

    public required int Value { get; init; }

    public required int Value2 { get; init; }
}
