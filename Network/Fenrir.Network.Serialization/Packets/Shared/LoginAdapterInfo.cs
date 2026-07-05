using Fenrir.Network.Abstractions;
using Fenrir.Network.Serialization.Attributes;

namespace Fenrir.Network.Serialization.Packets.Shared;

[FenrirWireType(156)]
public readonly partial record struct LoginAdapterInfo : IFenrirWireType<LoginAdapterInfo>
{
    [FixedString(128)] public required string AdapterName { get; init; }
    public required uint PhysicalAddressLength { get; init; }
    [FixedArray(8)] public required byte[] PhysicalAddress { get; init; }
    [FixedString(16)] public required string IPAddress { get; init; }
}
