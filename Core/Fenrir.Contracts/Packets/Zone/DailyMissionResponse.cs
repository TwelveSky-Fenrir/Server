using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

// Counters echoed are AFTER deduction; the mission item itself is delivered via a separate packet sent before this one.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DailyMission,
    ExpectedSize = 25)]
public readonly partial record struct DailyMissionResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Result { get; init; }
    public required MissionDate Mission { get; init; }
}
