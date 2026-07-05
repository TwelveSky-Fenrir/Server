using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

// C++ sizeof is 52 bytes; the trailing 2-byte pad can't be expressed here ([Reserved] only covers
// leading padding) and is instead carried by the parent packet's next field.
[FenrirWireType(50)]
public readonly partial record struct ProxyStateInfo : IFenrirWireType<ProxyStateInfo>
{
    [FixedArray(3)] public required float[] Location { get; init; }

    [FixedString(13)] public required string Name { get; init; }

    [FixedString(25)] public required string PshopName { get; init; }
}
