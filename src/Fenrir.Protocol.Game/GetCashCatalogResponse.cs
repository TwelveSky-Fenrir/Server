using Fenrir.Core.Attributes;
using Fenrir.Core.Wire;

namespace Fenrir.Protocol.Game;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.GetCashCatalog,
    ExpectedSize = 12809)]
public readonly partial record struct GetCashCatalogResponse : IOutgoingPacket
{
    public required int Result { get; init; }
    public required int Version { get; init; }
    [FixedArray(3200)] public required int[] CashItemInfo { get; init; }
}
