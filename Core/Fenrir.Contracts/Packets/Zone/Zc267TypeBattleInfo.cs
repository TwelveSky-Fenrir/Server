using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_267_TYPE_BATTLE_INFO (ZONE.h:974-978) — "zone 267" map event tick, S07_MyGame01.cpp:12190,
///     <c>BroadcastServer(1)</c>. Distinct C++ struct from ZC 100 (not a shared typedef) despite the
///     identical layout — dedicated record per spec.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Zone267TypeBattleInfo,
    ExpectedSize = 21)]
public readonly partial record struct Zc267TypeBattleInfo : IOutgoingPacket
{
    [FixedArray(4)] public required int[] BattleInfo { get; init; }
    public required int RemainTime { get; init; }
}
