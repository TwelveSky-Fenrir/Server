using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TowerStatus,
    ExpectedSize = 97)]
public readonly record struct TowerStatusResponse : IOutgoingPacket
{
    [FixedArray(12)] public required int[] State1Tower { get; init; }
    [FixedArray(12)] public required int[] State2Tower { get; init; }
}
