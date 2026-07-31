using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(280)]
public readonly partial record struct BuffInfo : IFenrirWireType<BuffInfo>
{
    [FixedArray(70)] public required int[] Buff { get; init; }
}
