using Fenrir.Core.Attributes;

namespace Fenrir.Core.Packets.Shared;

[FenrirWireType(50)]
public readonly partial record struct ProxyStateInfo : IFenrirWireType<ProxyStateInfo>
{
    [FixedArray(3)] public required float[] Location { get; init; }

    [FixedString(13)] public required string Name { get; init; }

    [FixedString(25)] public required string PshopName { get; init; }
}
