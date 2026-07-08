using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

/// <summary>Data's meaning depends on Sort; opaque payload here.</summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.ZoneEventInfo,
    ExpectedSize = 135)]
public readonly record struct ZoneEventInfoResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    [FixedArray(130)] public required byte[] Data { get; init; }
}
