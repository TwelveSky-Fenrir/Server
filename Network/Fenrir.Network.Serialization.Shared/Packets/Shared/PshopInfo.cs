using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(1232)]
public readonly partial record struct PshopInfo : IFenrirWireType<PshopInfo>
{
    public required uint UniqueNumber { get; init; }

    [FixedString(25)] public required string Name { get; init; }

    [Reserved(3)] [FixedArray(225)] public required int[] ItemInfo { get; init; }

    [FixedArray(75)] public required int[] SocketInfo { get; init; }
}
