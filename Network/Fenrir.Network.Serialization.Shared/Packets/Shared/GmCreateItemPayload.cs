using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmCreateItemPayload : IFenrirWireType<GmCreateItemPayload>
{

        public required int ItemId { get; init; }
}
