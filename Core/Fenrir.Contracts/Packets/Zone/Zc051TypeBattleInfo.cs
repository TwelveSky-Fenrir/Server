using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_051_TYPE_BATTLE_INFO (ZONE.h:930-934) — "zone 051" territory-stone battle tick, builder
///     S05_MyTransfer.cpp:1152, broadcast (S07_MyGame01, 1 emission). Same shape as ZC 95/100/103 but a
///     dedicated record per opcode, per spec.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Zone051TypeBattleInfo,
    ExpectedSize = 21)]
public readonly partial record struct Zc051TypeBattleInfo : IOutgoingPacket
{
    [FixedArray(4)] public required int[] ExistStone { get; init; }
    public required int RemainTime { get; init; }
}
