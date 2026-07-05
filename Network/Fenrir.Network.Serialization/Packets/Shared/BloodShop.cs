using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(604)]
public readonly partial record struct BloodShop : IFenrirWireType<BloodShop>
{
    // Count of valid entries (<=50); slots beyond it are still sent on the wire as zeros.
    public required int BloodNum { get; init; }

    [FixedArray(50)] public required BloodItem[] Data { get; init; }
}
