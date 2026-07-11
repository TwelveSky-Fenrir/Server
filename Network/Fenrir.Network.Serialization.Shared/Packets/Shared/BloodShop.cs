using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(604)]
public readonly partial record struct BloodShop : IFenrirWireType<BloodShop>
{
    public required int BloodNum { get; init; }

    [FixedArray(50)] public required BloodItem[] Data { get; init; }
}
