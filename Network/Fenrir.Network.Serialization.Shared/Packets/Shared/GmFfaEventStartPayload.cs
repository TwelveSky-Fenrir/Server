using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmFfaEventStartPayload : IFenrirWireType<GmFfaEventStartPayload>
{

        public required int Time { get; init; }
}
