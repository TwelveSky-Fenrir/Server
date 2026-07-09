using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(604)]
public readonly record struct BloodShop : IFenrirWireType<BloodShop>
{
    // Count of valid entries (<=50); slots beyond it are still sent on the wire as zeros.
    public required int BloodNum { get; init; }

    [FixedArray(50)] public required BloodItem[] Data { get; init; }
}
