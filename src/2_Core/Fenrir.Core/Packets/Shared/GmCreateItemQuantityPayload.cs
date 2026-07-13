using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(8)]
public readonly partial record struct GmCreateItemQuantityPayload : IFenrirWireType<GmCreateItemQuantityPayload>
{
    public required int ItemId { get; init; }

    public required int Quantity { get; init; }
}
