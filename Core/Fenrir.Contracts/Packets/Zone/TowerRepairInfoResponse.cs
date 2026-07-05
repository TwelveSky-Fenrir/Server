using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZCP_BROADCAST_CHUGSOUNG_REPAIR_INFO (153, <c>ZONE.h:1234-1237</c>): announces to the whole tower's zone
///     which avatar just repaired the guardian monster's HP. <c>B_BROADCAST_CHUGSOUNG_REPAIR_INFO</c>
///     (<c>S05_MyTransfer.cpp:1570-1574</c>) is defined but never called anywhere in the traced source (item-667
///     "repair" use never reaches it) -- same class of dead broadcast as <see cref="TowerStatusResponse" />'s own
///     zero live callers. Kept as real production API for whichever future cluster wires the item-667 repair path.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.TowerRepairInfo, ExpectedSize = 14)]
public readonly partial record struct TowerRepairInfoResponse : IOutgoingPacket
{
    [FixedString(13)] public required string AvatarName { get; init; }
}
