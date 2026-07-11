using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(12)]
public readonly partial record struct GmMoveCoordinatePayload : IFenrirWireType<GmMoveCoordinatePayload>
{
    [FixedArray(3)] public required float[] Location { get; init; }
}
