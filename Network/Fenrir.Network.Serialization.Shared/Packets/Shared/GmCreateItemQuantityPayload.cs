using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(8)]
public readonly partial record struct GmCreateItemQuantityPayload : IFenrirWireType<GmCreateItemQuantityPayload>
{
    public required int ItemId { get; init; }

    public required int Quantity { get; init; }
}
