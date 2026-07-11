using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(4)]
public readonly partial record struct GmLevelSetPayload : IFenrirWireType<GmLevelSetPayload>
{
    public required int Level { get; init; }
}
