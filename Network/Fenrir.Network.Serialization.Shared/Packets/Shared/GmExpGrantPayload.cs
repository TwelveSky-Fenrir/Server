using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(8)]
public readonly partial record struct GmExpGrantPayload : IFenrirWireType<GmExpGrantPayload>
{

        public required int Type { get; init; }

        public required int Exp { get; init; }
}
