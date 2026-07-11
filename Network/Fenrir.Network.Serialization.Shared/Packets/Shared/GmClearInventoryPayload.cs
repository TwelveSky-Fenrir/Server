using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmClearInventoryPayload : IFenrirWireType<GmClearInventoryPayload>
{
    public required int PageSelector { get; init; }
}
