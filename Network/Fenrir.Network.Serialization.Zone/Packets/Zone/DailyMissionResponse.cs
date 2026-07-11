using Fenrir.Network.Serialization.Shared.Packets.Shared;
using Fenrir.Network.Serialization.Wire;
using Fenrir.Network.Serialization.Wire.Attributes;

namespace Fenrir.Network.Serialization.Zone.Packets.Zone;

[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DailyMission,
    ExpectedSize = 25)]
public readonly partial record struct DailyMissionResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Result { get; init; }
    public required MissionDate Mission { get; init; }
}
