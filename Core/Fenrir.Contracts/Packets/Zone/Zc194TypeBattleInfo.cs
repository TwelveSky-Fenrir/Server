using Fenrir.Contracts.Abstractions;
using Fenrir.Contracts.Attributes;
using Fenrir.Contracts.Wire;

namespace Fenrir.Contracts.Packets.Zone;

/// <summary>
///     ZC_194_TYPE_BATTLE_INFO (ZONE.h:946-950) — "zone 194" inter-tribe war map event tick,
///     S07_MyGame01.cpp:10316, <c>BroadcastServer(1)</c>.
/// </summary>
[FenrirPacket(FenrirServer.Zone, FenrirDirection.Outgoing, Opcodes.Zone.Outgoing.Zone194TypeBattleInfo,
    ExpectedSize = 21)]
public readonly partial record struct Zc194TypeBattleInfo : IOutgoingPacket
{
    [FixedArray(4)] public required int[] BattleInfo { get; init; }
    public required int RemainTime { get; init; }
}
