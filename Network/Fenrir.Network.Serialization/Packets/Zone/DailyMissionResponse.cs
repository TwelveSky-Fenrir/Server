using Fenrir.Network.Serialization.Attributes;
using Fenrir.Network.Serialization.Packets.Shared;
using Fenrir.Network.Serialization.Wire;

namespace Fenrir.Network.Serialization.Packets.Zone;

// Counters echoed are AFTER deduction; the mission item itself is delivered via a separate SetInventorySlotResponse
// (ZC_SET_INVENTORY_ITEM_RECV, opcode 194) sent before this one -- see DailyMissionService.ClaimAsync's own remarks.
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DailyMission,
    ExpectedSize = 25)]
public readonly record struct DailyMissionResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Result { get; init; }
    public required MissionDate Mission { get; init; }
}
