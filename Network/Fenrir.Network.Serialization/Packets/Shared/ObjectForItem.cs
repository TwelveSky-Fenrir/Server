using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(84)]
public readonly record struct ObjectForItem : IFenrirWireType<ObjectForItem>
{
    public required int Index { get; init; }

    public required int Quantity { get; init; }

    public required int Value { get; init; }

    public required int SerialNumber { get; init; }

    [FixedArray(3)] public required float[] Location { get; init; }

    [FixedString(13)] public required string Master { get; init; }

    [FixedString(13)] public required string PartyName { get; init; }

    // 2-byte pad after PartyName (char[13]) to align DropSort.
    [Reserved(2)] public required int DropSort { get; init; }

    public required uint CreateTime { get; init; }

    public required uint PresentTime { get; init; }

    public required int CreateState { get; init; }

    // Always zero on the wire: USE_SOCKET_GEM is #undef in EU33, but the field is still transmitted.
    [FixedArray(3)] public required int[] SocketGem { get; init; }
}
