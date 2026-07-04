using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>Data's meaning depends on Sort; opaque payload here.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneEventInfo,
    ExpectedSize = 135)]
public readonly partial record struct ZoneEventInfoResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedArray(130)] public required byte[] Data { get; init; }
}
