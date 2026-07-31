using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(84)]
public readonly partial record struct ObjectForItem : IFenrirWireType<ObjectForItem>
{
    public required int Index { get; init; }

    public required int Quantity { get; init; }

    public required int Value { get; init; }

    public required int SerialNumber { get; init; }

    [FixedArray(3)] public required float[] Location { get; init; }

    [FixedString(13)] public required string Master { get; init; }

    [FixedString(13)] public required string PartyName { get; init; }

    [Reserved(2)] public required int DropSort { get; init; }

    public required uint CreateTime { get; init; }

    public required uint PresentTime { get; init; }

    public required int CreateState { get; init; }

    [FixedArray(3)] public required int[] SocketGem { get; init; }
}
