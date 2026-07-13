using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(12)]
public readonly partial record struct GmMoveCoordinatePayload : IFenrirWireType<GmMoveCoordinatePayload>
{
    [FixedArray(3)] public required float[] Location { get; init; }
}
