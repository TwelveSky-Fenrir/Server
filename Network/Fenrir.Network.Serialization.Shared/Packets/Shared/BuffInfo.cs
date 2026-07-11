using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Shared.Packets.Shared;

[FenrirWireType(280)]
public readonly partial record struct BuffInfo : IFenrirWireType<BuffInfo>
{
    [FixedArray(70)] public required int[] Buff { get; init; }
}
