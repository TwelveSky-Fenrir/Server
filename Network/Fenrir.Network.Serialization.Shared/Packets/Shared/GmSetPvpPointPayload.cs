using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(8)]
public readonly partial record struct GmSetPvpPointPayload : IFenrirWireType<GmSetPvpPointPayload>
{
    public required int DuelSlot { get; init; }

    public required int PointValue { get; init; }
}
