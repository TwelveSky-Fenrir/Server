using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(8)]
public readonly partial record struct GmSetPvpPointPayload : IFenrirWireType<GmSetPvpPointPayload>
{
    public required int DuelSlot { get; init; }

    public required int PointValue { get; init; }
}
