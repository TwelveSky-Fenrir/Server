using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Packets.Shared;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_MISSION_COMPLETE_RECV (ZONE.h:1268-1273) — builder <c>B_MISSION_COMPLETE_RECV</c>
///     (S05_MyTransfer.cpp:1605); reply to CZ 126 (S04_MyWork02.cpp:14615), always emitted at the end
///     of the handler (echoes <c>tSort</c> + remaining counters AFTER deduction). The mission item is
///     delivered separately via ZC 119 (MULTI_ITEM_CREATE, <c>tNum = 7001 + iCount</c>) emitted BEFORE
///     this packet.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.DailyMission,
    ExpectedSize = 25)]
public readonly partial record struct DailyMissionResponse : IOutgoingPacket
{
    public required int Sort { get; init; }
    public required int Result { get; init; }
    public required MissionDate Mission { get; init; }
}
