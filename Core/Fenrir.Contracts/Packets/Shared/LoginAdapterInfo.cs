using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;

namespace Fenrir.Contracts.Packets.Shared;

[FenrirWireType(156)]
public readonly partial record struct LoginAdapterInfo : IFenrirWireType<LoginAdapterInfo>
{
    [FixedString(128)] public required string AdapterName { get; init; }
    public required uint PhysicalAddressLength { get; init; }
    [FixedArray(8)] public required byte[] PhysicalAddress { get; init; }
    [FixedString(16)] public required string IPAddress { get; init; }
}
